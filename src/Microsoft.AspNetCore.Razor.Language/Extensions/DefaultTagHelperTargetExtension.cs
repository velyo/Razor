﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Legacy;

namespace Microsoft.AspNetCore.Razor.Language.Extensions
{
    internal sealed class DefaultTagHelperTargetExtension : IDefaultTagHelperTargetExtension
    {
        private static readonly string[] PrivateModifiers = new string[] { "private" };

        public bool DesignTime { get; set; }

        public string RunnerVariableName { get; set; } = "__tagHelperRunner";

        public string StringValueBufferVariableName { get; set; } = "__tagHelperStringValueBuffer";

        public string CreateTagHelperMethodName { get; set; } = "CreateTagHelper";

        public string ExecutionContextTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.Runtime.TagHelpers.TagHelperExecutionContext";

        public string ExecutionContextVariableName { get; set; } = "__tagHelperExecutionContext";

        public string ExecutionContextAddMethodName { get; set; } = "Add";

        public string TagHelperRunnerTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.Runtime.TagHelpers.TagHelperRunner";

        public string ExecutionContextOutputPropertyName { get; set; } = "Output";

        public string ExecutionContextSetOutputContentAsyncMethodName { get; set; } = "SetOutputContentAsync";

        public string ExecutionContextAddHtmlAttributeMethodName { get; set; } = "AddHtmlAttribute";

        public string ExecutionContextAddTagHelperAttributeMethodName { get; set; } = "AddTagHelperAttribute";

        public string RunnerRunAsyncMethodName { get; set; } = "RunAsync";

        public string ScopeManagerTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.Runtime.TagHelpers.TagHelperScopeManager";

        public string ScopeManagerVariableName { get; set; } = "__tagHelperScopeManager";

        public string ScopeManagerBeginMethodName { get; set; } = "Begin";

        public string ScopeManagerEndMethodName { get; set; } = "End";

        public string StartTagHelperWritingScopeMethodName { get; set; } = "StartTagHelperWritingScope";

        public string EndTagHelperWritingScopeMethodName { get; set; } = "EndTagHelperWritingScope";

        public string TagModeTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.TagHelpers.TagMode";

        public string HtmlAttributeValueStyleTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.TagHelpers.HtmlAttributeValueStyle";

        public string TagHelperOutputIsContentModifiedPropertyName { get; set; } = "IsContentModified";

        public string BeginAddHtmlAttributeValuesMethodName { get; set; } = "BeginAddHtmlAttributeValues";

        public string EndAddHtmlAttributeValuesMethodName { get; set; } = "EndAddHtmlAttributeValues";

        public string BeginWriteTagHelperAttributeMethodName { get; set; } = "BeginWriteTagHelperAttribute";

        public string EndWriteTagHelperAttributeMethodName { get; set; } = "EndWriteTagHelperAttribute";

        public string MarkAsHtmlEncodedMethodName { get; set; } = "Html.Raw";

        public string FormatInvalidIndexerAssignmentMethodName { get; set; } = "InvalidTagHelperIndexerAssignment";

        public string WriteTagHelperOutputMethod { get; set; } = "Write";

        public void WriteTagHelperBody(CodeRenderingContext context, DefaultTagHelperBodyIntermediateNode node)
        {
            if (context.Parent as TagHelperIntermediateNode == null)
            {
                var message = Resources.FormatIntermediateNodes_InvalidParentNode(node.GetType(), typeof(TagHelperIntermediateNode));
                throw new InvalidOperationException(message);
            }

            if (DesignTime)
            {
                context.RenderChildren(node);
            }
            else
            {
                // Call into the tag helper scope manager to start a new tag helper scope.
                // Also capture the value as the current execution context.
                context.CodeWriter
                    .WriteStartAssignment(ExecutionContextVariableName)
                    .WriteStartInstanceMethodCall(
                        ScopeManagerVariableName,
                        ScopeManagerBeginMethodName);

                // Assign a unique ID for this instance of the source HTML tag. This must be unique
                // per call site, e.g. if the tag is on the view twice, there should be two IDs.
                var uniqueId = (string)context.Items[CodeRenderingContext.SuppressUniqueIds];
                if (uniqueId == null)
                {
                    uniqueId = Guid.NewGuid().ToString("N");
                }

                context.CodeWriter.WriteStringLiteralExpression(node.TagName)
                    .WriteParameterSeparator()
                    .Write(TagModeTypeName)
                    .Write(".")
                    .Write(node.TagMode.ToString())
                    .WriteParameterSeparator()
                    .WriteStringLiteralExpression(uniqueId)
                    .WriteParameterSeparator();

                using (context.CodeWriter.BuildAsyncLambdaExpression())
                {
                    // We remove and redirect writers so TagHelper authors can retrieve content.
                    context.RenderChildren(node, new RuntimeNodeWriter());
                }

                context.CodeWriter.WriteEndMethodCall();
            }
        }

        public void WriteTagHelperCreate(CodeRenderingContext context, DefaultTagHelperCreateIntermediateNode node)
        {
            if (context.Parent as TagHelperIntermediateNode == null)
            {
                var message = Resources.FormatIntermediateNodes_InvalidParentNode(node.GetType(), typeof(TagHelperIntermediateNode));
                throw new InvalidOperationException(message);
            }

            context.CodeWriter
                .WriteStartAssignment(node.Field)
                .Write(CreateTagHelperMethodName)
                .WriteLine("<global::" + node.Type + ">();");

            if (!DesignTime)
            {
                context.CodeWriter.WriteInstanceMethodCall(
                    ExecutionContextVariableName,
                    ExecutionContextAddMethodName,
                    node.Field);
            }
        }

        public void WriteTagHelperExecute(CodeRenderingContext context, DefaultTagHelperExecuteIntermediateNode node)
        {
            if (context.Parent as TagHelperIntermediateNode == null)
            {
                var message = Resources.FormatIntermediateNodes_InvalidParentNode(node.GetType(), typeof(TagHelperIntermediateNode));
                throw new InvalidOperationException(message);
            }

            if (!DesignTime)
            {
                context.CodeWriter
                    .Write("await ")
                    .WriteStartInstanceMethodCall(
                        RunnerVariableName,
                        RunnerRunAsyncMethodName)
                    .Write(ExecutionContextVariableName)
                    .WriteEndMethodCall();

                var tagHelperOutputAccessor = $"{ExecutionContextVariableName}.{ExecutionContextOutputPropertyName}";

                context.CodeWriter
                    .Write("if (!")
                    .Write(tagHelperOutputAccessor)
                    .Write(".")
                    .Write(TagHelperOutputIsContentModifiedPropertyName)
                    .WriteLine(")");

                using (context.CodeWriter.BuildScope())
                {
                    context.CodeWriter
                        .Write("await ")
                        .WriteInstanceMethodCall(
                            ExecutionContextVariableName,
                            ExecutionContextSetOutputContentAsyncMethodName);
                }

                context.CodeWriter
                    .WriteStartMethodCall(WriteTagHelperOutputMethod)
                    .Write(tagHelperOutputAccessor)
                    .WriteEndMethodCall()
                    .WriteStartAssignment(ExecutionContextVariableName)
                    .WriteInstanceMethodCall(
                        ScopeManagerVariableName,
                        ScopeManagerEndMethodName);
            }
        }

        public void WriteTagHelperHtmlAttribute(CodeRenderingContext context, DefaultTagHelperHtmlAttributeIntermediateNode node)
        {
            if (context.Parent as TagHelperIntermediateNode == null)
            {
                var message = Resources.FormatIntermediateNodes_InvalidParentNode(node.GetType(), typeof(TagHelperIntermediateNode));
                throw new InvalidOperationException(message);
            }

            if (DesignTime)
            {
                context.RenderChildren(node);
            }
            else
            {
                var attributeValueStyleParameter = $"{HtmlAttributeValueStyleTypeName}.{node.AttributeStructure}";
                var isConditionalAttributeValue = node.Children.Any(
                    child => child is CSharpExpressionAttributeValueIntermediateNode || child is CSharpCodeAttributeValueIntermediateNode);

                // All simple text and minimized attributes will be pre-allocated.
                if (isConditionalAttributeValue)
                {
                    // Dynamic attribute value should be run through the conditional attribute removal system. It's
                    // unbound and contains C#.

                    // TagHelper attribute rendering is buffered by default. We do not want to write to the current
                    // writer.
                    var valuePieceCount = node.Children.Count(
                        child =>
                            child is HtmlAttributeValueIntermediateNode ||
                            child is CSharpExpressionAttributeValueIntermediateNode ||
                            child is CSharpCodeAttributeValueIntermediateNode ||
                            child is ExtensionIntermediateNode);

                    context.CodeWriter
                        .WriteStartMethodCall(BeginAddHtmlAttributeValuesMethodName)
                        .Write(ExecutionContextVariableName)
                        .WriteParameterSeparator()
                        .WriteStringLiteralExpression(node.AttributeName)
                        .WriteParameterSeparator()
                        .Write(valuePieceCount.ToString(CultureInfo.InvariantCulture))
                        .WriteParameterSeparator()
                        .Write(attributeValueStyleParameter)
                        .WriteEndMethodCall();

                    context.RenderChildren(node, new TagHelperHtmlAttributeRuntimeNodeWriter());

                    context.CodeWriter.WriteMethodCallExpressionStatement(
                            EndAddHtmlAttributeValuesMethodName,
                            ExecutionContextVariableName);
                }
                else
                {
                    // This is a data-* attribute which includes C#. Do not perform the conditional attribute removal or
                    // other special cases used when IsDynamicAttributeValue(). But the attribute must still be buffered to
                    // determine its final value.

                    // Attribute value is not plain text, must be buffered to determine its final value.
                    context.CodeWriter.WriteMethodCallExpressionStatement(BeginWriteTagHelperAttributeMethodName);

                    // We're building a writing scope around the provided chunks which captures everything written from the
                    // page. Therefore, we do not want to write to any other buffer since we're using the pages buffer to
                    // ensure we capture all content that's written, directly or indirectly.
                    context.RenderChildren(node, new RuntimeNodeWriter());

                    context.CodeWriter
                        .WriteStartAssignment(StringValueBufferVariableName)
                        .WriteMethodCallExpressionStatement(EndWriteTagHelperAttributeMethodName)
                        .WriteStartInstanceMethodCall(
                            ExecutionContextVariableName,
                            ExecutionContextAddHtmlAttributeMethodName)
                        .WriteStringLiteralExpression(node.AttributeName)
                        .WriteParameterSeparator()
                        .WriteStartMethodCall(MarkAsHtmlEncodedMethodName)
                        .Write(StringValueBufferVariableName)
                        .WriteEndMethodCall(endLine: false)
                        .WriteParameterSeparator()
                        .Write(attributeValueStyleParameter)
                        .WriteEndMethodCall();
                }
            }
        }

        public void WriteTagHelperProperty(CodeRenderingContext context, DefaultTagHelperPropertyIntermediateNode node)
        {
            var tagHelperNode = context.Parent as TagHelperIntermediateNode;
            if (context.Parent == null)
            {
                var message = Resources.FormatIntermediateNodes_InvalidParentNode(node.GetType(), typeof(TagHelperIntermediateNode));
                throw new InvalidOperationException(message);
            }

            if (!DesignTime)
            {
                // Ensure that the property we're trying to set has initialized its dictionary bound properties.
                if (node.IsIndexerNameMatch &&
                    object.ReferenceEquals(FindFirstUseOfIndexer(tagHelperNode, node), node))
                {
                    // Throw a reasonable Exception at runtime if the dictionary property is null.
                    context.CodeWriter.WriteFormatLine($"if ({node.Field}.{node.Property} == null)");
                    using (context.CodeWriter.BuildScope())
                    {
                        // System is in Host.NamespaceImports for all MVC scenarios. No need to generate FullName
                        // of InvalidOperationException type.
                        context.CodeWriter.WriteFormatLine(
                            $@"throw new InvalidOperationException({FormatInvalidIndexerAssignmentMethodName}(""{node.AttributeName}"", ""{node.TagHelper.GetTypeName()}"", ""{ node.Property}""));");
                    }
                }
            }

            // If this is not the first use of the attribute value, we need to evaluate the expression and assign it to
            // the tag helper property.
            //
            // Otherwise, the value has already been computed and assigned to another tag helper. We just need to
            // copy from that tag helper to this one.
            //
            // This is important because we can't evaluate the expression twice due to side-effects.
            var firstUseOfAttribute = FindFirstUseOfAttribute(tagHelperNode, node);
            if (!object.ReferenceEquals(firstUseOfAttribute, node))
            {
                // If we get here, this value has already been used. We just need to copy the value.
                using (context.CodeWriter.BuildAssignmentStatement(GetPropertyAccessor(node)))
                {
                    context.CodeWriter.Write(GetPropertyAccessor(firstUseOfAttribute));
                }

                return;
            }

            // If we get there, this is the first time seeing this property so we need to evaluate the expression.
            if (node.BoundAttribute.IsStringProperty || (node.IsIndexerNameMatch && node.BoundAttribute.IsIndexerStringProperty))
            {
                if (DesignTime)
                {
                    context.RenderChildren(node);

                    using (context.CodeWriter.BuildAssignmentStatement(GetPropertyAccessor(node)))
                    {
                        if (node.Children.Count == 1 && node.Children.First() is HtmlContentIntermediateNode htmlNode)
                        {
                            var content = GetContent(htmlNode);
                            context.CodeWriter.WriteStringLiteralExpression(content);
                        }
                        else
                        {
                            context.CodeWriter.Write("string.Empty");
                        }
                    }
                }
                else
                {
                    context.CodeWriter.WriteMethodCallExpressionStatement(BeginWriteTagHelperAttributeMethodName);

                    context.RenderChildren(node, new LiteralRuntimeNodeWriter());

                    using (context.CodeWriter.BuildAssignmentStatement(StringValueBufferVariableName))
                    {
                        context.CodeWriter.WriteMethodCallExpressionStatement(EndWriteTagHelperAttributeMethodName);
                    }
                    using (context.CodeWriter.BuildAssignmentStatement(StringValueBufferVariableName))
                    {
                        context.CodeWriter.Write(StringValueBufferVariableName);
                    }
                }
            }
            else
            {
                if (DesignTime)
                {
                    var firstMappedChild = node.Children.FirstOrDefault(child => child.Source != null) as IntermediateNode;
                    var valueStart = firstMappedChild?.Source;

                    using (context.CodeWriter.BuildLineDirective(node.Source))
                    {
                        var accessor = GetPropertyAccessor(node);
                        var assignmentPrefixLength = accessor.Length + " = ".Length;
                        if (node.BoundAttribute.IsEnum &&
                            node.Children.Count == 1 &&
                            node.Children.First() is IntermediateToken token &&
                            token.IsCSharp)
                        {
                            assignmentPrefixLength += $"global::{node.BoundAttribute.TypeName}.".Length;

                            if (valueStart != null)
                            {
                                context.CodeWriter.WritePadding(assignmentPrefixLength, node.Source, context);
                            }

                            context.CodeWriter
                                .WriteStartAssignment(accessor)
                                .Write("global::")
                                .Write(node.BoundAttribute.TypeName)
                                .Write(".");
                        }
                        else
                        {
                            if (valueStart != null)
                            {
                                context.CodeWriter.WritePadding(assignmentPrefixLength, node.Source, context);
                            }

                            context.CodeWriter.WriteStartAssignment(GetPropertyAccessor(node));
                        }

                        RenderTagHelperAttributeInline(context, node, node.Source);

                        context.CodeWriter.WriteLine(";");
                    }
                }
                else
                {
                    using (context.CodeWriter.BuildLineDirective(node.Source))
                    {
                        context.CodeWriter.WriteStartAssignment(GetPropertyAccessor(node));

                        if (node.BoundAttribute.IsEnum &&
                            node.Children.Count == 1 &&
                            node.Children.First() is IntermediateToken token &&
                            token.IsCSharp)
                        {
                            context.CodeWriter
                                .Write("global::")
                                .Write(node.BoundAttribute.TypeName)
                                .Write(".");
                        }

                        RenderTagHelperAttributeInline(context, node, node.Source);

                        context.CodeWriter.WriteLine(";");
                    }
                }
            }

            if (!DesignTime)
            {
                // We need to inform the context of the attribute value.
                context.CodeWriter
                    .WriteStartInstanceMethodCall(
                        ExecutionContextVariableName,
                        ExecutionContextAddTagHelperAttributeMethodName)
                    .WriteStringLiteralExpression(node.AttributeName)
                    .WriteParameterSeparator()
                    .Write(GetPropertyAccessor(node))
                    .WriteParameterSeparator()
                    .Write($"global::Microsoft.AspNetCore.Razor.TagHelpers.HtmlAttributeValueStyle.{node.AttributeStructure}")
                    .WriteEndMethodCall();
            }
        }

        public void WriteTagHelperRuntime(CodeRenderingContext context, DefaultTagHelperRuntimeIntermediateNode node)
        {
            if (!DesignTime)
            {
                context.CodeWriter.WriteLine("#line hidden");

                // Need to disable the warning "X is never used." for the value buffer since
                // whether it's used depends on how a TagHelper is used.
                context.CodeWriter.WriteLine("#pragma warning disable 0169");
                context.CodeWriter.WriteFieldDeclaration(PrivateModifiers, "string", StringValueBufferVariableName);
                context.CodeWriter.WriteLine("#pragma warning restore 0169");

                context.CodeWriter.WriteFieldDeclaration(PrivateModifiers, ExecutionContextTypeName, ExecutionContextVariableName);

                context.CodeWriter
                    .Write("private ")
                    .Write(TagHelperRunnerTypeName)
                    .Write(" ")
                    .Write(RunnerVariableName)
                    .Write(" = new ")
                    .Write(TagHelperRunnerTypeName)
                    .WriteLine("();");

                var backedScopeManageVariableName = "__backed" + ScopeManagerVariableName;
                context.CodeWriter.WriteFieldDeclaration(PrivateModifiers, ScopeManagerTypeName, backedScopeManageVariableName, "null");

                context.CodeWriter
                .Write("private ")
                .Write(ScopeManagerTypeName)
                .Write(" ")
                .WriteLine(ScopeManagerVariableName);

                using (context.CodeWriter.BuildScope())
                {
                    context.CodeWriter.WriteLine("get");
                    using (context.CodeWriter.BuildScope())
                    {
                        context.CodeWriter
                            .Write("if (")
                            .Write(backedScopeManageVariableName)
                            .WriteLine(" == null)");

                        using (context.CodeWriter.BuildScope())
                        {
                            context.CodeWriter
                                .WriteStartAssignment(backedScopeManageVariableName)
                                .WriteStartNewObject(ScopeManagerTypeName)
                                .Write(StartTagHelperWritingScopeMethodName)
                                .WriteParameterSeparator()
                                .Write(EndTagHelperWritingScopeMethodName)
                                .WriteEndMethodCall();
                        }

                        context.CodeWriter
                            .Write("return ")
                            .Write(backedScopeManageVariableName)
                            .WriteLine(";");
                    }
                }
            }
        }

        private void RenderTagHelperAttributeInline(
            CodeRenderingContext context,
            DefaultTagHelperPropertyIntermediateNode property,
            SourceSpan? span)
        {
            for (var i = 0; i < property.Children.Count; i++)
            {
                RenderTagHelperAttributeInline(context, property, property.Children[i], span);
            }
        }

        private void RenderTagHelperAttributeInline(
            CodeRenderingContext context,
            DefaultTagHelperPropertyIntermediateNode property,
            IntermediateNode node,
            SourceSpan? span)
        {
            if (node is CSharpExpressionIntermediateNode || node is HtmlContentIntermediateNode)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    RenderTagHelperAttributeInline(context, property, node.Children[i], span);
                }
            }
            else if (node is IntermediateToken token)
            {
                if (DesignTime && node.Source != null)
                {
                    context.AddLineMappingFor(node);
                }

                context.CodeWriter.Write(token.Content);
            }
            else if (node is CSharpCodeIntermediateNode)
            {
                var error = new RazorError(
                    LegacyResources.TagHelpers_CodeBlocks_NotSupported_InAttributes,
                    SourceLocation.FromSpan(span),
                    span == null ? -1 : span.Value.Length);
                context.Diagnostics.Add(RazorDiagnostic.Create(error));
            }
            else if (node is TemplateIntermediateNode)
            {
                var expectedTypeName = property.IsIndexerNameMatch ? property.BoundAttribute.IndexerTypeName : property.BoundAttribute.TypeName;
                var error = new RazorError(
                    LegacyResources.FormatTagHelpers_InlineMarkupBlocks_NotSupported_InAttributes(expectedTypeName),
                    SourceLocation.FromSpan(span),
                    span == null ? -1 : span.Value.Length);
                context.Diagnostics.Add(RazorDiagnostic.Create(error));
            }
        }

        private static DefaultTagHelperPropertyIntermediateNode FindFirstUseOfIndexer(
            TagHelperIntermediateNode tagHelperNode,
            DefaultTagHelperPropertyIntermediateNode propertyNode)
        {
            Debug.Assert(tagHelperNode.Children.Contains(propertyNode));
            Debug.Assert(propertyNode.IsIndexerNameMatch);

            for (var i = 0; i < tagHelperNode.Children.Count; i++)
            {
                if (tagHelperNode.Children[i] is DefaultTagHelperPropertyIntermediateNode otherPropertyNode &&
                    otherPropertyNode.TagHelper.Equals(propertyNode.TagHelper) &&
                    otherPropertyNode.BoundAttribute.Equals(propertyNode.BoundAttribute) &&
                    otherPropertyNode.IsIndexerNameMatch)
                {
                    return otherPropertyNode;
                }
            }

            // This is unreachable, we should find 'propertyNode' in the list of children.
            throw new InvalidOperationException();
        }

        private static DefaultTagHelperPropertyIntermediateNode FindFirstUseOfAttribute(
            TagHelperIntermediateNode tagHelperNode,
            DefaultTagHelperPropertyIntermediateNode propertyNode)
        {
            for (var i = 0; i < tagHelperNode.Children.Count; i++)
            {
                if (tagHelperNode.Children[i] is DefaultTagHelperPropertyIntermediateNode otherPropertyNode &&
                    string.Equals(otherPropertyNode.AttributeName, propertyNode.AttributeName, StringComparison.Ordinal))
                {
                    return otherPropertyNode;
                }
            }

            // This is unreachable, we should find 'propertyNode' in the list of children.
            throw new InvalidOperationException();
        }

        private string GetContent(HtmlContentIntermediateNode node)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is IntermediateToken token && token.IsHtml)
                {
                    builder.Append(token.Content);
                }
            }

            return builder.ToString();
        }

        private static string GetPropertyAccessor(DefaultTagHelperPropertyIntermediateNode node)
        {
            var propertyAccessor = $"{node.Field}.{node.Property}";

            if (node.IsIndexerNameMatch)
            {
                var dictionaryKey = node.AttributeName.Substring(node.BoundAttribute.IndexerNamePrefix.Length);
                propertyAccessor += $"[\"{dictionaryKey}\"]";
            }

            return propertyAccessor;
        }
    }
}
