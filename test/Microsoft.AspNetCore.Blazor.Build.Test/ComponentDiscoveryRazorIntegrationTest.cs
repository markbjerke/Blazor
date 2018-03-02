// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using Microsoft.AspNetCore.Blazor.Test.Helpers;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.AspNetCore.Blazor.Build.Test
{
    public class ComponentDiscoveryRazorIntegrationTest : RazorIntegrationTestBase
    {
        internal override bool UseTwoPhaseCompilation => true;

        [Fact]
        public void ComponentDiscovery_CanFindComponent_DefinedinCSharp()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(CSharpSyntaxTree.ParseText(@"
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
    }
}
"));

            // Act
            var result = CompileToCSharp("@addTagHelper *, TestAssembly");

            // Assert
            var bindings = result.CodeDocument.GetTagHelperContext();
            Assert.Single(bindings.TagHelpers, t => t.Name == "Test.MyComponent");
        }

        [Fact]
        public void ComponentDiscovery_CanFindComponent_DefinedinCshtml()
        {
            // Arrange

            // Act
            var result = CompileToCSharp("UniqueName.cshtml", "@addTagHelper *, TestAssembly");

            // Assert
            var bindings = result.CodeDocument.GetTagHelperContext();
            Assert.Single(bindings.TagHelpers, t => t.Name == "Test.UniqueName");
        }

        [Fact]
        public void ComponentDiscovery_CanFindComponent_BuiltIn()
        {
            // Arrange

            // Act
            var result = CompileToCSharp("@addTagHelper *, Microsoft.AspNetCore.Blazor");

            // Assert
            var bindings = result.CodeDocument.GetTagHelperContext();
            Assert.Single(bindings.TagHelpers, t => t.Name == "Microsoft.AspNetCore.Blazor.Routing.NavLink");
        }

        [Fact]
        public void ComponentDiscovery_CanRenderComponent_WithMinimizedAttribute()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(CSharpSyntaxTree.ParseText(@"
using System.Text;
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
        public bool BoolProperty { get; set; }
    }
}"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
@using System.Text
<MyComponent BoolProperty />");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 2, 0),
                frame => AssertFrame.Attribute(frame, "BoolProperty", true, 1));
        }
    }
}
