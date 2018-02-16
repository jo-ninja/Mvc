﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Microsoft.AspNetCore.Mvc.TagHelpers
{
    /// <summary>
    /// Renders a partial view.
    /// </summary>
    [HtmlTargetElement("partial", Attributes = "name", TagStructure = TagStructure.WithoutEndTag)]
    public class PartialTagHelper : TagHelper
    {
        private const string ForAttributeName = "asp-for";
        private const string ViewDataDictionaryName = "asp-all-viewdata";
        private const string ViewDataValuesPrefix = "asp-viewdata-";
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IViewBufferScope _viewBufferScope;
        private IDictionary<string, object> _viewDataValues;

        public PartialTagHelper(
            ICompositeViewEngine viewEngine,
            IViewBufferScope viewBufferScope)
        {
            _viewEngine = viewEngine ?? throw new ArgumentNullException(nameof(viewEngine));
            _viewBufferScope = viewBufferScope ?? throw new ArgumentNullException(nameof(viewBufferScope));
        }

        /// <summary>
        /// The name or path of the partial view that is rendered to the response.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// An expression to be evaluated against the current model.
        /// </summary>
        [HtmlAttributeName(ForAttributeName)]
        public ModelExpression For { get; set; }

        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        /// <summary>
        /// Values for <see cref="ViewDataDictionary"/>.
        /// </summary>
        [HtmlAttributeName(ViewDataDictionaryName, DictionaryAttributePrefix = ViewDataValuesPrefix)]
        public IDictionary<string, object> ViewData
        {
            get
            {
                if (_viewDataValues == null)
                {
                    _viewDataValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }

                return _viewDataValues;
            }
            set
            {
                _viewDataValues = value;
            }
        }

        /// <inheritdoc />
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var viewBuffer = new ViewBuffer(_viewBufferScope, Name, ViewBuffer.PartialViewPageSize);
            using (var writer = new ViewBufferTextWriter(viewBuffer, Encoding.UTF8))
            {
                await RenderPartialViewAsync(writer);

                // Reset the TagName. We don't want `partial` to render.
                output.TagName = null;
                output.Content.SetHtmlContent(viewBuffer);
            }
        }

        private async Task RenderPartialViewAsync(TextWriter writer)
        {
            var viewEngineResult = _viewEngine.GetView(ViewContext.ExecutingFilePath, Name, isMainPage: false);
            var getViewLocations = viewEngineResult.SearchedLocations;
            if (!viewEngineResult.Success)
            {
                viewEngineResult = _viewEngine.FindView(ViewContext, Name, isMainPage: false);
            }

            if (!viewEngineResult.Success)
            {
                var searchedLocations = Enumerable.Concat(getViewLocations, viewEngineResult.SearchedLocations);
                var locations = string.Empty;
                if (searchedLocations.Any())
                {
                    locations += Environment.NewLine + string.Join(Environment.NewLine, searchedLocations);
                }

                throw new InvalidOperationException(
                    Resources.FormatViewEngine_PartialViewNotFound(Name, locations));
            }

            var view = viewEngineResult.View;

            // Use the rendering View's model only if an asp-for expression does not exist
            var model = For != null ? For.Model : ViewContext.ViewData.Model;
            var newViewData = new ViewDataDictionary<object>(ViewContext.ViewData, model);
            if (_viewDataValues != null)
            {
                foreach (var kvp in _viewDataValues)
                {
                    newViewData[kvp.Key] = kvp.Value;
                }
            }

            var partialViewContext = new ViewContext(ViewContext, view, newViewData, writer);

            if (For?.Name != null)
            {
                newViewData.TemplateInfo.HtmlFieldPrefix = newViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
            }

            using (view as IDisposable)
            {
                await view.RenderAsync(partialViewContext);
            }
        }
    }
}
