using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IO.Compression;
using System.IO;

namespace GitRelease {

	public class Git {
		private HttpClient HClient { get; set; }
		public string Repository { get; }
		public string Project { get; }
		public string? Zip { get; private set; }
		public async Task<GitArtifacts> GetArtifacts() {
			try {
				using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/{Project}/actions/artifacts?per_page=20");
				using var res = await HClient.SendAsync(req);
				var ret = await res.Content.ReadFromJsonAsync<GitArtifacts>(new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }) ?? new();
				ret.StatusCode = (int)res.StatusCode;
				ret.Error = !res.IsSuccessStatusCode;
				return ret;

			} catch (Exception ex) {
				return await Task.FromResult(new GitArtifacts() { Error = true, StatusCode = -1, Message = ex.Message });
			}
		}
		public async Task Download(Artifact art, string? path=null) {
			var pth = string.IsNullOrEmpty(path) ? Path.Combine(Helper.Path, "Artifacts") : path;
			if (!Directory.Exists(pth)) Directory.CreateDirectory(pth);

			using var s = await HClient.GetStreamAsync(art.Download);
			using var fs = new FileStream(Zip = Path.Combine(pth, $"{art.Name}.zip"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
			await s.CopyToAsync(fs);
		}

		public void Unzip(string path) => ZipFile.ExtractToDirectory(Zip ?? "", path, true);

		public Git(string repo, string proj, string? token = null) {
			HClient = new HttpClient();
			var hdr = HClient.DefaultRequestHeaders;
			if (!string.IsNullOrEmpty(token)) hdr.Authorization = new("Bearer", token);
			hdr.UserAgent.Add(new("RusysDev-Client", "0.1"));
			hdr.Accept.Add(new("application/json"));
			Repository = repo;
			Project = proj;
		}
	}



	public class GitArtifacts {
		[JsonPropertyName("total_count")] public int Total { get; set; }
		public List<Artifact>? Artifacts { get; set; }
		public string? Message { get; set; }
		public bool Error { get; set; }
		public int StatusCode { get; set; }
	}

	public class Workflow {
		public long Id { get; set; }
		[JsonPropertyName("head_branch")] public string? Branch { get; set; }
	}
	public class Artifact {
		public long Id { get; set; }
		public string? Name { get; set; }
		public string Title => $"{Name} ({Created:yyyy-MM-dd HH:mm:ss}) - {Size.GetFileSizeSuffix(2)}";
		[JsonPropertyName("workflow_run")] public Workflow? Workflow { get; set; }
		[JsonPropertyName("created_at")] public DateTime Created { get; set; }
		[JsonPropertyName("updated_at")] public DateTime Updated { get; set; }
		[JsonPropertyName("size_in_bytes")] public long Size { get; set; }
		[JsonPropertyName("archive_download_url")] public string? Download { get; set; }
	}




	public static class Formatting {
		private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
		public static string GetFileSizeSuffix(this long value, int decimalPlaces = 1) {
			if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException(nameof(decimalPlaces)); }
			if (value < 0) { return "-" + GetFileSizeSuffix(-value); }
			if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }
			var mag = (int)Math.Log(value, 1024);
			var adjustedSize = (decimal)value / (1L << (mag * 10));
			if (Math.Round(adjustedSize, decimalPlaces) >= 1000) { mag += 1; adjustedSize /= 1024; }
			return string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, SizeSuffixes[mag]);
		}
	}


}
