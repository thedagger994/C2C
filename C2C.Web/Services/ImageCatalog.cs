using System.Net;
using Microsoft.AspNetCore.Html;

namespace C2C.Web.Services;

/// <summary>Builds responsive img tags from the pre-generated {name}-{width}.webp files in wwwroot/img.</summary>
public class ImageCatalog
{
    private readonly Dictionary<string, int[]> _widths;

    public ImageCatalog(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.WebRootPath, "img");
        _widths = Directory.GetFiles(dir, "*.webp")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Select(n => (name: n[..n.LastIndexOf('-')], w: int.Parse(n[(n.LastIndexOf('-') + 1)..])))
            .GroupBy(x => x.name)
            .ToDictionary(g => g.Key, g => g.Select(x => x.w).OrderBy(x => x).ToArray());
    }

    public IHtmlContent Tag(string name, string alt, string sizes = "100vw", string? cls = null, bool eager = false)
    {
        var w = _widths[name];
        var srcset = string.Join(", ", w.Select(x => $"/img/{name}-{x}.webp {x}w"));
        var src = $"/img/{name}-{w[Math.Min(1, w.Length - 1)]}.webp";
        var c = cls is null ? "" : $" class=\"{WebUtility.HtmlEncode(cls)}\"";
        var load = eager ? " fetchpriority=\"high\"" : " loading=\"lazy\" decoding=\"async\"";
        return new HtmlString($"<img src=\"{src}\" srcset=\"{srcset}\" sizes=\"{sizes}\" alt=\"{WebUtility.HtmlEncode(alt)}\"{c}{load}>");
    }
}
