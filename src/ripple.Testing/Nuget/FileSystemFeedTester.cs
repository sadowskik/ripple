using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FubuCore;
using FubuTestingSupport;
using NUnit.Framework;
using NuGet;
using ripple.Model;
using ripple.Nuget;

namespace ripple.Testing.Nuget
{
	[TestFixture]
	public class FileSystemFeedTester
	{
		private string theDirectory;
		private FileSystem theFileSystem;
		private FileSystemNugetFeed theFeed;

		[TestFixtureSetUp]
		public void SetUp()
		{
			theDirectory = ".".AppendPath("local-feed").ToFullPath();
			theFileSystem = new FileSystem();

			theFileSystem.CreateDirectory(theDirectory);

			createNuget("FubuCore", "0.9.9.9");
			createNuget("FubuCore", "1.0.0.0");
			createNuget("Bottles", "1.0.0.0");

			createNuget("FubuNew", "1.0.0.0");
			createNuget("FubuNew", "1.0.0.1-alpha");

			theFeed = new FileSystemNugetFeed(theDirectory, NugetStability.ReleasedOnly);
		}

		private void createNuget(string id, string version)
		{
			theFileSystem.WriteStringToFile(Path.Combine(theDirectory, "{0}.{1}.nupkg".ToFormat(id, version)), "");
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			theFileSystem.DeleteDirectory(theDirectory);
		}

		[Test]
		public void finds_each_nuget_at_the_specified_version()
		{
			theFeed.Find(new Dependency("FubuCore", "0.9.9.9")).ShouldNotBeNull();
			theFeed.Find(new Dependency("FubuCore", "1.0.0.0")).ShouldNotBeNull();
			theFeed.Find(new Dependency("Bottles", "1.0.0.0")).ShouldNotBeNull();
		}

		[Test, Explicit, Repeat(1000)]
		public void finds_the_same_nuget_at_the_specified_version_in_parallel()
		{
			stress_parallel(() =>
				theFeed.Find(new Dependency("FubuCore", "0.9.9.9")).ShouldNotBeNull());
		}

		[Test]
		public void finds_the_latest_of_each_nuget()
		{
			theFeed.FindLatest(new Dependency("FubuCore")).Version.ShouldEqual(new SemanticVersion("1.0.0.0"));
			theFeed.FindLatest(new Dependency("Bottles")).Version.ShouldEqual(new SemanticVersion("1.0.0.0"));
		}
		
		[Test, Explicit, Repeat(100)]
		public void finds_the_latest_of_each_nuget_in_parallel()
		{
			stress_parallel(() =>
				theFeed.FindLatest(new Dependency("FubuCore")).Version.ShouldEqual(new SemanticVersion("1.0.0.0")));
		}

		[Test]
		public void finds_the_latest_for_a_special_version()
		{
			theFeed.FindLatest(new Dependency("FubuNew") { NugetStability = NugetStability.ReleasedOnly }).Version.ShouldEqual(new SemanticVersion("1.0.0.0"));
			theFeed.FindLatest(new Dependency("FubuNew") { NugetStability = NugetStability.Anything }).Version.ShouldEqual(new SemanticVersion("1.0.0.1-alpha"));
		}

		private static void stress_parallel(Action action, int interations = 1000, int parallelHint = 10)
		{
			IList<Task> tasks = new List<Task>(parallelHint);

			for (int i = 0; i < parallelHint; i++)
			{
				int shift = i;
				var searchTask = Task.Factory.StartNew(() =>
				{
					Thread.Sleep(shift%100);
					for (int j = 0; j < interations; j++)
						action();
				});

				tasks.Add(searchTask);
			}

			Task.WaitAll(tasks.ToArray());
		}
	}

	[TestFixture]
	public class FloatingFileSystemFeedTester
	{
		private string theDirectory;
		private FileSystem theFileSystem;
		private FloatingFileSystemNugetFeed theFeed;

		[TestFixtureSetUp]
		public void SetUp()
		{
			theDirectory = ".".AppendPath("local-feed").ToFullPath();
			theFileSystem = new FileSystem();

			theFileSystem.CreateDirectory(theDirectory);

			createNuget("FubuCore", "0.9.9.9");
			createNuget("fubuCore", "0.9.9.82819");
			createNuget("FubuCore", "1.0.0.0");
			createNuget("Bottles", "1.0.0.0");

			createNuget("ExtendHealth.Quoting.Imm.Data", "2.2.0.275");
			createNuget("ExtendHealth.Quoting.Imm.Data", "2.2.0.249");

			createNuget("FubuNew", "1.0.0.0");
			createNuget("FubuNew", "1.0.0.1-alpha");

			theFeed = new FloatingFileSystemNugetFeed(theDirectory, NugetStability.Anything);
		}

		private void createNuget(string id, string version)
		{
			theFileSystem.WriteStringToFile(Path.Combine(theDirectory, "{0}.{1}.nupkg".ToFormat(id, version)), "");
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			theFileSystem.DeleteDirectory(theDirectory);
		}

		[Test]
		public void finds_the_latest_of_all_nugets()
		{
			var nugets = theFeed.GetLatest().Select(x => "{0},{1}".ToFormat(x.Name, x.Version)).ToArray();
			nugets.ShouldHaveTheSameElementsAs("Bottles,1.0.0.0", "ExtendHealth.Quoting.Imm.Data,2.2.0.275", "FubuCore,1.0.0.0", "FubuNew,1.0.0.1-alpha");
		}
	}
}