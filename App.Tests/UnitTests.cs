using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.Tests;

[TestClass]
public class MainPageSmokeTests
{
    [TestMethod]
    public void MainPage_BuildOutputAndXamlContract_ShouldContainKeyWorkbenchControls()
    {
        var appAssemblyPath = ResolveBuiltAppAssemblyPath();
        var assembly = Assembly.LoadFrom(appAssemblyPath);
        var mainPageType = assembly.GetType("App.Views.MainPage");

        Assert.IsNotNull(mainPageType, $"未找到类型 App.Views.MainPage。程序集：{appAssemblyPath}");
        Assert.IsNotNull(mainPageType!.GetConstructor(Type.EmptyTypes), "MainPage 应保留无参构造函数。");

        var xamlPath = ResolveMainPageXamlPath();
        var xaml = XDocument.Load(xamlPath);
        var nameAttributes = xaml
            .Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName == "Name")
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "ImageModeButton",
                "UiModeButton",
                "DeviceList",
                "Canvas",
                "CaptureButton",
                "MatchSearchRegionTextBox"
            },
            nameAttributes.ToList(),
            $"MainPage.xaml 缺少关键控件。文件：{xamlPath}");
    }

    [TestMethod]
    public void MultiColorRawArguments_ShouldParseFindModeToolFormat()
    {
        var request = ParseMultiColorRawArguments(
            "\"#35ca1f\",[[1,11,\"#2cc71c\"],[0,27,\"#2fc623\"]],{region:[2181,558,16,63],threshold:[26]}");

        AssertRequestProperty(request, "Mode", "FindInRegion");
        AssertRequestProperty(request, "FirstColor", "#35ca1f");
        AssertRequestProperty(request, "Threshold", 26);
        AssertRegion(request, x: 2181, y: 558, width: 16, height: 63);
        AssertOffsetsCount(request, 2);
    }

    [TestMethod]
    public void MultiColorRawArguments_ShouldNormalizeX1Y1X2Y2Region()
    {
        var request = ParseMultiColorRawArguments(
            "2300,300,\"#35ca1f\",[[1,11,\"#2cc71c\"]],{region:[2137,257,2339,362],threshold:16}");

        AssertRequestProperty(request, "Mode", "DetectAtAnchor");
        AssertRequestProperty(request, "AnchorX", 2300);
        AssertRequestProperty(request, "AnchorY", 300);
        AssertRegion(request, x: 2137, y: 257, width: 202, height: 105);
        AssertOffsetsCount(request, 1);
    }

    [TestMethod]
    public void MatchSearchRegionInput_ShouldNormalizeX1Y1X2Y2Region()
    {
        var region = ParseOptionalRegion("2137,257,2339,362");

        Assert.IsNotNull(region);
        AssertRegionCoordinates(region!, x: 2137, y: 257, width: 202, height: 105);
    }

    private static string ResolveBuiltAppAssemblyPath()
    {
        var solutionRoot = GetSolutionRoot();
        var appBinDirectory = Path.Combine(solutionRoot, "App", "bin");

        Assert.IsTrue(Directory.Exists(appBinDirectory), $"未找到 App 输出目录：{appBinDirectory}");

        var candidate = Directory
            .EnumerateFiles(appBinDirectory, "autojs6-dev-tools.dll", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(GetAssemblyPathPriority)
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        Assert.IsFalse(string.IsNullOrWhiteSpace(candidate), $"未找到已构建的 App 程序集，请先构建解决方案。目录：{appBinDirectory}");

        return candidate!;
    }

    private static string ResolveMainPageXamlPath()
    {
        var solutionRoot = GetSolutionRoot();
        var xamlPath = Path.Combine(solutionRoot, "App", "Views", "MainPage.xaml");

        Assert.IsTrue(File.Exists(xamlPath), $"未找到 MainPage.xaml：{xamlPath}");

        return xamlPath;
    }

    private static string GetSolutionRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static int GetAssemblyPathPriority(string path)
    {
        if (path.Contains($"{Path.DirectorySeparatorChar}x64{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}ARM64{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static object ParseMultiColorRawArguments(string rawArguments)
    {
        var appAssemblyPath = ResolveBuiltAppAssemblyPath();
        var assembly = Assembly.LoadFrom(appAssemblyPath);
        var mainPageType = assembly.GetType("App.Views.MainPage");
        Assert.IsNotNull(mainPageType, $"未找到类型 App.Views.MainPage。程序集：{appAssemblyPath}");

        var method = mainPageType!.GetMethod(
            "ParseMultiColorRawArguments",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "未找到 ParseMultiColorRawArguments 方法。");

        try
        {
            var request = method!.Invoke(null, [rawArguments]);
            Assert.IsNotNull(request, "解析结果不应为空。");
            return request!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            Assert.Fail($"解析多点颜色参数失败：{ex.InnerException.Message}");
            throw;
        }
    }

    private static object? ParseOptionalRegion(string rawRegion)
    {
        var appAssemblyPath = ResolveBuiltAppAssemblyPath();
        var assembly = Assembly.LoadFrom(appAssemblyPath);
        var mainPageType = assembly.GetType("App.Views.MainPage");
        Assert.IsNotNull(mainPageType, $"未找到类型 App.Views.MainPage。程序集：{appAssemblyPath}");

        var method = mainPageType!.GetMethod(
            "ParseOptionalRegion",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "未找到 ParseOptionalRegion 方法。");

        try
        {
            return method!.Invoke(null, [rawRegion]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            Assert.Fail($"解析搜索区域失败：{ex.InnerException.Message}");
            throw;
        }
    }

    private static void AssertRequestProperty(object request, string propertyName, object expected)
    {
        var property = request.GetType().GetProperty(propertyName);
        Assert.IsNotNull(property, $"解析结果缺少属性：{propertyName}");
        Assert.AreEqual(expected.ToString(), property!.GetValue(request)?.ToString(), propertyName);
    }

    private static void AssertRegion(object request, int x, int y, int width, int height)
    {
        var region = request.GetType().GetProperty("Region")?.GetValue(request);
        Assert.IsNotNull(region, "解析结果应包含 region。");
        AssertRegionCoordinates(region!, x, y, width, height);
    }

    private static void AssertRegionCoordinates(object region, int x, int y, int width, int height)
    {
        AssertRequestProperty(region, "X", x);
        AssertRequestProperty(region, "Y", y);
        AssertRequestProperty(region, "Width", width);
        AssertRequestProperty(region, "Height", height);
    }

    private static void AssertOffsetsCount(object request, int expectedCount)
    {
        var offsets = request.GetType().GetProperty("Offsets")?.GetValue(request);
        Assert.IsNotNull(offsets, "解析结果应包含 offsets。");
        var count = ((System.Collections.IEnumerable)offsets!).Cast<object>().Count();
        Assert.AreEqual(expectedCount, count);
    }
}
