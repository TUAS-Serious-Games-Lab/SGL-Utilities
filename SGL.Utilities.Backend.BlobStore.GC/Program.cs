using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SGL.Utilities.Backend.BlobStore.GC {
	class Program {
		class Options {
			[Option('d', "dry-run", Group = "Mode", HelpText = "Display a list of files that would be removed, but don't actually delete them.")]
			public bool DryRun { get; set; } = false;
			[Option('f', "force", Group = "Mode", HelpText = "Actually permanently(!) delete all temporary files in the given blob repository.")]
			public bool Force { get; set; } = false;
			[Value(0, MetaName = "REPOSITORY_DIRECTORY", HelpText = "The blob storage directory to operate on.")]
			public string Directory { get; set; } = Path.Combine(Environment.CurrentDirectory, "BlobStorage");

			[Usage]
			public static IEnumerable<Example> Examples => new List<Example> {
				new Example("List temp files under repository ./BlobStorage that would be deleted",
					new UnParserSettings(){ PreferShortName = true},
					new Options() { DryRun = true, Directory = "./BlobStorage" }),
				new Example("Actually delete temp files under repository ./BlobStorage",
					new UnParserSettings(){ PreferShortName = true},
					new Options() { Force = true, Directory = "./BlobStorage" })
			};
		}
		static void Main(string[] args) => ((Action<ParserResult<Options>>)(res => res.WithParsed(RealMain).WithNotParsed(errs => DisplayHelp(res, errs))))(new Parser(c => c.HelpWriter = null).ParseArguments<Options>(args));

		static void DisplayHelp(ParserResult<Options> result, IEnumerable<Error> errs) {
			Console.WriteLine(HelpText.AutoBuild(result, h => {
				h.AdditionalNewLineAfterOption = false;
				h.Heading = $"SGL Utility BlobStore GC {Assembly.GetExecutingAssembly().GetName().Version}";
				h.MaximumDisplayWidth = 170;
				return h;
			}));
		}

		static void RealMain(Options opts) {
			var repo = new FileSystemBlobRepository(opts.Directory);
			var tempFiles = repo.EnumerateTempFiles().ToList();
			if (tempFiles.Count == 0) {
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("No temporary files found.");
				Console.ResetColor();
				return;
			}
			foreach (var file in tempFiles) {
				if (opts.DryRun || !opts.Force) {
					Console.ForegroundColor = ConsoleColor.Green;
					Console.Write("Would delete ");
					Console.ResetColor();
					Console.WriteLine(file);
				}
				else if (opts.Force) {
					Console.ForegroundColor = ConsoleColor.Red;
					Console.Write("Deleting ");
					Console.ResetColor();
					Console.Write(file);
					Console.Write(" ... ");
					repo.DeleteTempFile(file);
					Console.ForegroundColor = ConsoleColor.Blue;
					Console.WriteLine("done");
					Console.ResetColor();
				}
			}
		}
	}
}
