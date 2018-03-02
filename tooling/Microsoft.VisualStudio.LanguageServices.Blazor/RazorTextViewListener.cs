// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Blazor
{
    [ContentType(RazorLanguage.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Export(typeof(IWpfTextViewConnectionListener))]
    internal class BlazorOpenDocumentTracker : IWpfTextViewConnectionListener
    {
        private readonly RazorEditorFactoryService _editorFactory;
        private readonly Workspace _workspace;

        private readonly HashSet<IWpfTextView> _openViews;
        private readonly Type _trackerType;

        [ImportingConstructor]
        public BlazorOpenDocumentTracker(
            RazorEditorFactoryService editorFactory,
            [Import(typeof(VisualStudioWorkspace))] Workspace workspace)
        {
            if (editorFactory == null)
            {
                throw new ArgumentNullException(nameof(editorFactory));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            _editorFactory = editorFactory;
            _workspace = workspace;

            _openViews = new HashSet<IWpfTextView>();
            _trackerType = Type.GetType("");

            _workspace.WorkspaceChanged += Workspace_WorkspaceChanged;
        }

        public Workspace Workspace => _workspace;

        public void SubjectBuffersConnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
        {
            if (textView == null)
            {
                throw new ArgumentException(nameof(textView));
            }

            if (subjectBuffers == null)
            {
                throw new ArgumentNullException(nameof(subjectBuffers));
            }

            _openViews.Add(textView);
        }

        public void SubjectBuffersDisconnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
        {
            if (textView == null)
            {
                throw new ArgumentException(nameof(textView));
            }

            if (subjectBuffers == null)
            {
                throw new ArgumentNullException(nameof(subjectBuffers));
            }

            _openViews.Remove(textView);
        }

        // We're watching the Roslyn workspace for changes specifically because we want
        // to know when the language service has processed a file change.
        //
        // It might be more elegant to use a file watcher rather than sniffing workspace events
        // but there would be a delay between the file watcher and Roslyn processing the update.
        private void Workspace_WorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
        {
            switch (e.Kind)
            {
                case WorkspaceChangeKind.DocumentAdded:
                case WorkspaceChangeKind.DocumentChanged:
                case WorkspaceChangeKind.DocumentInfoChanged:
                case WorkspaceChangeKind.DocumentReloaded:
                case WorkspaceChangeKind.DocumentRemoved:

                    var document = e.NewSolution.GetDocument(e.DocumentId);
                    if (document == null || document.FilePath == null)
                    {
                        break;
                    }
                    
                    if (!document.FilePath.EndsWith(".g.i.cs"))
                    {
                        break;
                    }

                    OnDeclarationsChanged();
                    break;
            }
        }

        private void OnDeclarationsChanged()
        {
            // This is a design-time Razor file change.Go poke all of the open
            // Razor documents and tell them to update.
            var buffers = _openViews
                .SelectMany(v => v.BufferGraph.GetTextBuffers(b => b.ContentType.IsOfType("RazorCSharp")))
                .Distinct()
                .ToArray();

            foreach (var buffer in buffers)
            {
                if (_editorFactory.TryGetParser(buffer, out var parser))
                {
                    // fire a 'no-op' change - this will cause the WTE editor to update tag helpers.
                    var method = parser.GetType().GetMethod("OnDocumentStructureChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                    method.Invoke(parser, new object[]
                    {
                                new DocumentStructureChangedEventArgs(new SourceChange(SourceSpan.Undefined, string.Empty), buffer.CurrentSnapshot, parser.CodeDocument),
                    });
                }

                if (_editorFactory.TryGetDocumentTracker(buffer, out var tracker))
                {
                    var field = tracker.GetType().GetField("_project", BindingFlags.NonPublic | BindingFlags.Instance);
                    var snapshot = field.GetValue(tracker);

                    var method = tracker.GetType().GetMethod("OnContextChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                    method.Invoke(tracker, new object[] { snapshot, ContextChangeKind.ImportsChanged });
                }
            }
        }
    }
}