using RuinGamePDT.Generation;
using RuinGamePDT.Resources;
using RuinGamePDT.World;
using Xunit.Abstractions;

namespace RuinGamePDT.Tests;

public class WorldGenerationTests(ITestOutputHelper output)
{
    private static char BiomeChar(BiomeType biome) => biome switch
    {
        BiomeType.Mountains => '^',
        BiomeType.Desert    => '.',
        BiomeType.Swamp     => '~',
        BiomeType.Forest    => 'T',
        BiomeType.Plains    => '-',
        _                   => '?'
    };

    private static string BiomeCssClass(BiomeType biome) => biome switch
    {
        BiomeType.Mountains => "m",
        BiomeType.Desert    => "d",
        BiomeType.Swamp     => "s",
        BiomeType.Forest    => "f",
        BiomeType.Plains    => "p",
        _                   => ""
    };

    [Fact]
    public void GenerateWorld_PrintsAsciiMap()
    {
        const int width = 80;
        const int height = 40;
        const int seed = 42;

        var generator = new WorldGenerator();
        WorldData world = generator.GenerateWorld(width, height, seed);

        // GenerateWorld centers the generated patch on (0,0), so the tiles it
        // produced span [-offset, width - offset) in each axis.
        int offsetX = width / 2;
        int offsetY = height / 2;

        output.WriteLine($"World Map ({width}x{height}, seed: {seed})");
        output.WriteLine("Legend: ^ Mountains  . Desert  ~ Swamp  T Forest  - Plains");
        output.WriteLine(new string('-', width + 2));

        for (int y = -offsetY; y < height - offsetY; y++)
        {
            var row = new char[width];
            for (int x = -offsetX; x < width - offsetX; x++)
                row[x + offsetX] = BiomeChar(world.GetTile(x, y)!.Biome);
            output.WriteLine("|" + new string(row) + "|");
        }

        output.WriteLine(new string('-', width + 2));

        var biomeCounts = new Dictionary<BiomeType, int>();
        foreach (BiomeType b in Enum.GetValues<BiomeType>())
            biomeCounts[b] = 0;

        for (int x = -offsetX; x < width - offsetX; x++)
            for (int y = -offsetY; y < height - offsetY; y++)
                biomeCounts[world.GetTile(x, y)!.Biome]++;

        int total = width * height;
        output.WriteLine("Biome distribution:");
        foreach (var (biome, count) in biomeCounts)
            output.WriteLine($"  {biome,-12} {count,4} ({count * 100.0 / total:F1}%)");

        Assert.True(world.GetTile(0, 0) != null);
    }

    [Fact]
    public void GenerateWorld_WritesColoredHtmlMap()
    {
        const int width = 120;
        const int height = 60;
        const int seed = 42;

        var generator = new WorldGenerator();
        WorldData world = generator.GenerateWorld(width, height, seed);

        int offsetX = width / 2;
        int offsetY = height / 2;

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var outputDir = Path.Combine(repoRoot, "TestOutput");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "world_map.html");

        var biomeCounts = new Dictionary<BiomeType, int>();
        foreach (BiomeType b in Enum.GetValues<BiomeType>())
            biomeCounts[b] = 0;

        var html = new System.Text.StringBuilder();
        html.AppendLine("""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <title>World Map</title>
            <style>
              body  { background: #111; margin: 0; padding: 20px; }
              pre   { font-family: 'Courier New', monospace; font-size: 13px; line-height: 1.15; letter-spacing: 0.05em; }
              .m    { color: #9ca3af; } /* Mountains */
              .d    { color: #d4a827; } /* Desert    */
              .s    { color: #2dd4bf; } /* Swamp     */
              .f    { color: #4ade80; } /* Forest    */
              .p    { color: #bef264; } /* Plains    */
              .legend { color: #e5e7eb; font-family: 'Courier New', monospace; font-size: 13px; margin-bottom: 12px; }
              .legend span { margin-right: 20px; }
              h2    { color: #e5e7eb; font-family: monospace; margin-bottom: 8px; }
              .stats { color: #9ca3af; font-family: monospace; font-size: 12px; margin-top: 12px; }
            </style>
            </head>
            <body>
            """);

        html.AppendLine($"<h2>World Map &mdash; {width}&times;{height}, seed {seed}</h2>");
        html.AppendLine("<div class=\"legend\">");
        html.AppendLine("  <span class=\"m\">^ Mountains</span>");
        html.AppendLine("  <span class=\"d\">. Desert</span>");
        html.AppendLine("  <span class=\"s\">~ Swamp</span>");
        html.AppendLine("  <span class=\"f\">T Forest</span>");
        html.AppendLine("  <span class=\"p\">- Plains</span>");
        html.AppendLine("</div>");
        html.AppendLine("<pre>");

        for (int y = -offsetY; y < height - offsetY; y++)
        {
            for (int x = -offsetX; x < width - offsetX; x++)
            {
                var tile = world.GetTile(x, y)!;
                biomeCounts[tile.Biome]++;
                html.Append($"<span class=\"{BiomeCssClass(tile.Biome)}\">{BiomeChar(tile.Biome)}</span>");
            }
            html.AppendLine();
        }

        html.AppendLine("</pre>");
        html.AppendLine("<div class=\"stats\">");

        int total = width * height;
        foreach (var (biome, count) in biomeCounts)
            html.AppendLine($"<div>{biome,-12} {count,5} ({count * 100.0 / total:F1}%)</div>");

        html.AppendLine("</div></body></html>");

        File.WriteAllText(outputPath, html.ToString());

        output.WriteLine($"Map written to: {outputPath}");
        Assert.True(File.Exists(outputPath));
    }
}
