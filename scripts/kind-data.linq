<Query Kind="Program">
  <NuGetReference>HtmlAgilityPack</NuGetReference>
  <NuGetReference>System.ServiceModel.Syndication</NuGetReference>
  <Namespace>System.ServiceModel.Syndication</Namespace>
  <Namespace>HtmlAgilityPack</Namespace>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
</Query>

string url = "https://github.com/kubernetes-sigs/kind/releases.atom";
string output = @"C:\code\kind-data\kind-version-map.json";

void Main()
{
	var formatter = new Atom10FeedFormatter();
	using (XmlReader reader = XmlReader.Create(url))
	{
		formatter.ReadFrom(reader);
	}

	var releases = formatter.Feed.Items.Select(i => i.Content)
										.Select(c => c as TextSyndicationContent)
										.Where(c => c is not null && c.Text.Contains("kindest/node:v"))
										.Select(c => KindRelease.Parse(c.Text));
	
	var feed = releases.ToDictionary();
	
	var file = new FileInfo(output);
	var existing = new Dictionary<string, Dictionary<string, string>>();
	if(file.Exists)
	{
		using(var reader = file.OpenText())
		{
			var content = reader.ReadToEnd();
			existing = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(content);
		}
	}
	foreach(var key in feed.Keys)
	{
		if(existing.ContainsKey(key))
		{
			existing[key] = feed[key];
		}
		else
		{
			existing.Add(key, feed[key]);	
		}
	}
	if(!file.Directory.Exists) file.Directory.Create();
	using(var writer = file.CreateText())
	{
		var json = JsonSerializer.Serialize(existing, Extensions.Options);
		writer.Write(json);
	}
}

public class KindRelease
{
	public string Version { get; set; } = string.Empty;
	public Dictionary<string, string> Images { get; set;} = new();
	
	public static KindRelease Parse(string content)
	{
		var stub = content.Substring(0, 16);
		var versionMatch = Regex.Match(stub, @"v\d{1,2}\.\d{1,3}.\d{1,3}");
		var version = versionMatch.Value.Substring(1);
		var li = Regex.Matches(content, @"<li>[\d\.]{4}.*</li>");
		var lines = li.Select(lca =>lca.Value); //.Dump();
		var images = new Dictionary<string, string>();
		foreach(var line in lines)
		{
			var kube = Regex.Match(line, @"<li>[\d\.]{4}.*: ").Value;
			kube = kube.Substring(4, kube.Length-6).Trim();
			var img = Regex.Match(line, @"<code>.*</code>").Value;
			img = img.Substring(6, img.Length-13).Trim();
			if(!images.ContainsKey(kube))
			{
				images.Add(kube, img);				
			}
		}
		return new KindRelease
		{
			Version = version,
			Images = images
		};
	}
}

public static class Extensions
{
	public static JsonSerializerOptions Options => new JsonSerializerOptions
	{
		AllowTrailingCommas = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
		IgnoreReadOnlyFields = true,
		IgnoreReadOnlyProperties = true,
		IncludeFields = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = true	
	};
	
	public static Dictionary<string, Dictionary<string, string>> ToDictionary(this IEnumerable<KindRelease> releases)
	{
		return releases.ToDictionary(k=>k.Version, v=>v.Images);
	}
	
	public static string ToJson(this IEnumerable<KindRelease> releases)
	{
		var data = releases.ToDictionary();
		var json = JsonSerializer.Serialize(data, Options);
		return json;
	}
}