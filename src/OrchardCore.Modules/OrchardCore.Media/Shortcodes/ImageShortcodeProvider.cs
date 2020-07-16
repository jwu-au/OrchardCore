using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OrchardCore.Infrastructure.Html;
using OrchardCore.ResourceManagement;
using Shortcodes;

namespace OrchardCore.Media.Shortcodes
{
    public class ImageShortcodeProvider : IShortcodeProvider
    {
        private static readonly ValueTask<string> Null = new ValueTask<string>((string)null);
        private static readonly ValueTask<string> ImageShortcode = new ValueTask<string>("[image]");
        private static readonly HashSet<string> Shortcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image",
            "media" // [media] is a deprecated shortcode, and can be removed in a future release.
        };

        private readonly IMediaFileStore _mediaFileStore;
        private readonly IHtmlSanitizerService _htmlSanitizerService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ResourceManagementOptions _options;

        public ImageShortcodeProvider(
            IMediaFileStore mediaFileStore,
            IHtmlSanitizerService htmlSanitizerService,
            IHttpContextAccessor httpContextAccessor,
            IOptions<ResourceManagementOptions> options)
        {
            _mediaFileStore = mediaFileStore;
            _htmlSanitizerService = htmlSanitizerService;
            _httpContextAccessor = httpContextAccessor;
            _options = options.Value;
        }

        public ValueTask<string> EvaluateAsync(string identifier, Arguments arguments, string content)
        {
            if (!Shortcodes.Contains(identifier))
            {
                return Null;
            }

            // Handle self closing shortcodes.
            if (String.IsNullOrEmpty(content))
            {
                content = arguments.NamedOrDefault("src");
                if (String.IsNullOrEmpty(content))
                {
                    // Do not handle the deprecated media shortcode in this edge case.
                    return ImageShortcode;
                }
            }

            if (!content.StartsWith("//", StringComparison.Ordinal) && !content.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Serve static files from virtual path.
                if (content.StartsWith("~/", StringComparison.Ordinal))
                {
                    content = _httpContextAccessor.HttpContext.Request.PathBase.Add(content.Substring(1)).Value;
                    if (!String.IsNullOrEmpty(_options.CdnBaseUrl))
                    {
                        content = _options.CdnBaseUrl + content;
                    }
                }
                else
                {
                    content = _mediaFileStore.MapPathToPublicUrl(content);
                }
            }

            if (arguments.Any())
            {
                var queryStringParams = new Dictionary<string, string>();

                var width = arguments.Named("width");
                var height = arguments.Named("height");
                var mode = arguments.Named("mode");

                if (width != null)
                {
                    queryStringParams.Add("width", width);
                }

                if (height != null)
                {
                    queryStringParams.Add("height", height);
                }

                if (mode != null)
                {
                    queryStringParams.Add("rmode", mode);
                }

                content = QueryHelpers.AddQueryString(content, queryStringParams);
            }

            content = "<img src=\"" + content + "\">";
            content = _htmlSanitizerService.Sanitize(content);

            return new ValueTask<string>(content);
        }
    }
}
