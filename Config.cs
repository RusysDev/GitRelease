using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace GitRelease {
	public class GitProfile {
		[XmlAttribute] public string? Repo { get; set; }
		[XmlAttribute] public string? Project { get; set; }
		[XmlAttribute] public string? Token { get; set; }
	}
	public class GitLast {
		[XmlAttribute] public string? Name { get; set; }
		[XmlAttribute] public DateTime Date { get; set; }
	}
	public class GitVersion {
		[XmlAttribute] public int Keep { get; set; }
		[XmlAttribute] public string? Pref { get; set; }
		[XmlAttribute] public string? Exec { get; set; }
	}
	public class Profile {
		[XmlAttribute] public string? Name { get; set; }
		[XmlAttribute] public bool Release { get; set; }
		public GitProfile? Git { get; set; }
		public string? Path { get; set; }
		public GitLast? Last { get; set; }
		public GitVersion? Versions { get; set; }
		public string? Branch { get; set; }
		public string? PreRelease { get; set; }
		public string? PostRelease { get; set; }
	}
	public class Publishing {
		public string? Token { get; set; }
		[XmlElement("Profile")] public List<Profile> Profiles { get; set; } = new();
	}

	public class Config {
		public string? Token { get; set; }
		public List<Profile>? Profiles { get; set; }
		public Profile? Selected { get; set; }
		public CfgError? Error { get; set; }
		public string CfgFile { get; set; }
		public Config(string file, string? name = null) {
			try {
				CfgFile = file;
				if (!File.Exists(file)) {
					var pth = Helper.Path;
					if (File.Exists($"{file}.xml")) file = $"{file}.xml";
					else if (File.Exists(Path.Combine(pth, $"{file}.xml"))) file = Path.Combine(pth, $"{file}.xml");
					else if (File.Exists(Path.Combine(pth, "config.xml"))) { name = file; file = Path.Combine(pth, "config.xml"); }
				}

				if (!File.Exists(file)) { Error = new("Config File", $"Unable to locate configuration file {file}"); return; }

				using var rdr = new StreamReader(CfgFile = new FileInfo(file).FullName);
				var mtd = (Publishing?)new XmlSerializer(typeof(Publishing)).Deserialize(rdr);

				if (mtd is not null) {
					Profiles = mtd.Profiles;
					Token = mtd.Token;

					if (!string.IsNullOrEmpty(name)) {
						foreach (var i in Profiles) if (i.Name == name) { Selected = i; break; }
					}
					else if (Profiles.Count == 1) Selected = Profiles.FirstOrDefault();
				}
				else Error = new("Read XML", "Unable to serialize XML file");
			} catch (Exception ex) {
				Error = new("Read XML", ex.Message, ex.StackTrace);
				throw;
			}
		}

		public void Save() {
			XmlSerializer serializer = new XmlSerializer(typeof(Publishing));
			using StreamWriter writer = new StreamWriter(CfgFile);
			var streamWriter = XmlWriter.Create(writer, new() {
				Encoding = Encoding.UTF8,
				Indent = true, IndentChars = "\t"
			});

			serializer.Serialize(streamWriter, new Publishing() { Profiles = Profiles ?? new(), Token = Token });
		}
	}

	public class CfgError {
		public string? Error { get; set; }
		public string? Message { get; set; }
		public string? StackTrace { get; set; }
		public CfgError(string err, string msg, string? trace = null) {
			Error = err; Message = msg; StackTrace = trace;
		}
	}

	public static class Helper {
		public static string Path => System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
		public static string? Bash(this string cmd) {
			Console.Write("Running sctipts.");
			try {
				var escapedArgs = cmd.Replace("\"", "\\\"");

				var process = new Process() {
					StartInfo = new ProcessStartInfo {
						FileName = "/bin/bash",
						Arguments = $"-c \"{escapedArgs}\"",
						RedirectStandardOutput = true,
						UseShellExecute = false,
						CreateNoWindow = true,
					}
				};

				process.Start();
				string result = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				Console.WriteLine("");
				return result;
			} catch (Exception ex) {
				Console.WriteLine($" Error: {ex.Message}");
				return null;
			}
		}
	}
}
