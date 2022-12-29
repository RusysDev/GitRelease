using GitRelease;

//dotnet publish -c Release -o ./release --runtime linux-arm64 --self-contained false

var select = 0;
var arg = args.Length;

var cfg = new Config(arg > 0 ? args[0] : "config.xml", arg > 1 ? args[1] : "");

if (cfg.Error is not null) Console.WriteLine($"Configuration error:\n\t{cfg.Error.Message} ({cfg.Error.Error})");
else if (cfg.Profiles is null || !cfg.Profiles.Any()) Console.WriteLine($"Configuration error:\n\tNo profiles found.");
else {
	var slx = cfg.Selected;

	if (slx is null) {
		var inc = 0;
		Console.WriteLine("Configuration profiles:");
		foreach (var i in cfg.Profiles) {
			inc++;
			Console.WriteLine($"\t{inc}: {i.Name} [{i.Git?.Repo}\\{i.Git?.Project}]");
		}
		Console.WriteLine("");
		while (slx is null) {
			Console.Write("Select profile: ");
			if (int.TryParse(Console.ReadLine(), out var sel) && sel > 0 && sel <= cfg.Profiles.Count)
				slx = cfg.Profiles[sel - 1];
		}
		Console.WriteLine("");
	}

	if (slx.Git is null || string.IsNullOrEmpty(slx.Git.Repo) || string.IsNullOrEmpty(slx.Git.Project))
		Console.WriteLine($"Configuration error:\n\tMissing Git configuration");

	else {
		var git = new Git(slx.Git.Repo, slx.Git.Project, slx.Git.Token ?? cfg.Token);

		Console.WriteLine($"Using:\n\tRepo: {slx.Git.Repo}\n\tProj: {slx.Git.Project}\n");

		var art = git.GetArtifacts().Result;

		if (!string.IsNullOrEmpty(slx.Branch) && art.Artifacts is not null) {
			var fltr = new GitArtifacts() { Artifacts = new() };
			foreach (var i in art.Artifacts) if (i.Workflow?.Branch == slx.Branch) fltr.Artifacts.Add(i);
			fltr.Total = fltr.Artifacts.Count;
			art = fltr;
		}

		if (art.Error) Console.WriteLine($"Connection error:\n\t{art.Message} ({art.StatusCode})");
		else if (art.Artifacts is null || art.Total <= 0) Console.WriteLine("No artifacts found.");
		else {
			Artifact itm;
			if (slx.Release) {
				itm = art.Artifacts.First();
				if (slx.Last is null || slx.Last.Date == DateTime.MinValue) {
					Console.WriteLine($"Current release is not available. Getting latest.");
				}
				else {
					Console.WriteLine($"Current release: {slx.Last.Name} ({slx.Last.Date:yyyy-MM-dd HH:mm:ss})");
					if (itm.Created <= slx.Last.Date) {
						Console.WriteLine($"Latest artifact: {itm.Title}");
						Console.WriteLine($"You already have latest release.");
						return;
					}
				}
			}
			else {
				Console.WriteLine($"Artifacts ({art.Total}):");
				var inc = 0;
				foreach (var i in art.Artifacts) {
					inc++;
					Console.WriteLine($"\t{inc}: {i.Title}");
				}
				Console.WriteLine("\n");
				while (select > inc || select < 1) {
					Console.Write("Select artifact: ");
					select = int.TryParse(Console.ReadLine(), out var sel) ? sel : 0;
				}
				itm = art.Artifacts[select - 1];
			}

			Console.WriteLine($"\nArtifact: {itm.Title}\nDownloading: {itm.Download}");
			await git.Download(itm, cfg.Artifacts);

			while (!Directory.Exists(slx.Path)) {
				if (!string.IsNullOrEmpty(slx.Path)) { Console.WriteLine("Destination path does not exist."); }
				Console.Write("Select output path: ");
				slx.Path = Console.ReadLine();
			}
			slx.Path = new DirectoryInfo(slx.Path).FullName;

			if (!string.IsNullOrEmpty(slx.PreRelease) && slx.PreRelease.Replace("${path}", slx.Path).Bash() is null) return;

			if (string.IsNullOrEmpty(git.Zip)) Console.WriteLine("Error: Artifact file not found.");
			else {
				var pth = slx.Path;
				Console.WriteLine($"Extracting: {pth}");
				if (slx.Versions is not null) { pth = Path.Combine(pth, itm.Name ?? ""); }
				git.Unzip(pth);

				if (slx.Versions is not null) {
					if (slx.Versions.Keep > 0) {
						var dirs = Directory.GetDirectories(slx.Path);
						var directory = new DirectoryInfo(slx.Path);
						var query = directory.GetDirectories(slx.Versions.Pref + "*", SearchOption.TopDirectoryOnly);
						foreach (var file in query.OrderByDescending(file => file.CreationTime).Skip(slx.Versions.Keep)) { file.Delete(true); }
					}

					if (!string.IsNullOrEmpty(slx.Versions.Exec)) {
						using var fs = File.Create(Path.Combine(slx.Path, "run"));
						var dt = new System.Text.UTF8Encoding(true).GetBytes($"#!/bin/bash\ncd \"{slx.Path}\"\n{Path.Combine(itm.Name ?? "", slx.Versions.Exec) + " " +
							(string.IsNullOrEmpty(slx.Versions.Args) ? "$*" : slx.Versions.Args)}\n");
						fs.Write(dt);

					}
				}


				if (!string.IsNullOrEmpty(slx.PostRelease) && slx.PostRelease.Replace("${path}", slx.Path).Bash() is null) return;

				if (slx.Release) {
					Console.WriteLine($"Saving configuration: {Path.GetFileName(cfg.CfgFile)}");
					slx.Last = new() { Name = itm.Name, Date = itm.Created }; cfg.Save();
				}

			}


		}
	}
}

Console.WriteLine("Done.");
