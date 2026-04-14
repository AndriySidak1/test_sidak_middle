using System.Text.RegularExpressions;

namespace CommentsApp.Api.Services;

public interface ICommentSanitizer
{
    bool TrySanitize(string input, out string sanitized);
}

/// <summary>
/// Sanitizes user input by allowing only the following tags:
///   &lt;i&gt; &lt;strong&gt; &lt;code&gt;  (no attributes)
///   &lt;a href="https://..." title="..."&gt;  (href required, title optional)
/// All other HTML is entity-encoded. Tag nesting is validated (XHTML).
/// </summary>
public sealed class CommentSanitizer : ICommentSanitizer
{
    // -- patterns that operate on the HtmlEncoded string -----------------------

    // <i>, <strong>, <code>  and their closing counterparts — NO attributes
    private static readonly Regex EncodedSimpleTag = new(
        @"&lt;(/?)(i|strong|code)\s*&gt;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // <a href="https://..."> with optional title="..."   (href is REQUIRED)
    private static readonly Regex EncodedAnchorOpen = new(
        @"&lt;a\s+href=&quot;(https?://[^&quot;]{1,2000})&quot;(?:\s+title=&quot;([^&quot;]{0,300})&quot;)?\s*&gt;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // </a>
    private static readonly Regex EncodedAnchorClose = new(
        @"&lt;/a\s*&gt;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // -- pattern that operates on the decoded (final) string ------------------

    // Used only for nesting validation — matches every allowed tag variant
    private static readonly Regex DecodedTagRegex = new(
        @"<(/?)(i|strong|code|a)(?:\s+href=""https?://[^""]{1,2000}"")?(?:\s+title=""[^""]{0,300}"")?\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // -------------------------------------------------------------------------

    public bool TrySanitize(string input, out string sanitized)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            sanitized = string.Empty;
            return false;
        }

        // 1. Encode everything — kills all raw HTML / XSS vectors
        var encoded = System.Net.WebUtility.HtmlEncode(input);

        // 2. Selectively decode only the whitelisted tag patterns
        var result = EncodedSimpleTag.Replace(encoded, m => System.Net.WebUtility.HtmlDecode(m.Value));
        result = EncodedAnchorOpen.Replace(result, m => System.Net.WebUtility.HtmlDecode(m.Value));
        result = EncodedAnchorClose.Replace(result, m => System.Net.WebUtility.HtmlDecode(m.Value));

        // 3. Validate tag nesting — reject if not valid XHTML
        if (!IsValidXhtmlNesting(result))
        {
            sanitized = string.Empty;
            return false;
        }

        sanitized = result;
        return true;
    }

    private static bool IsValidXhtmlNesting(string text)
    {
        var stack = new Stack<string>();

        foreach (Match match in DecodedTagRegex.Matches(text))
        {
            var isClosing = match.Groups[1].Value == "/";
            var tag = match.Groups[2].Value.ToLowerInvariant();

            // Reject any tag that is not in the allowed list
            if (tag is not ("i" or "strong" or "code" or "a"))
            {
                return false;
            }

            if (!isClosing)
            {
                stack.Push(tag);
            }
            else
            {
                if (stack.Count == 0 || stack.Pop() != tag)
                {
                    return false;
                }
            }
        }

        // All opened tags must be closed
        return stack.Count == 0;
    }
}
