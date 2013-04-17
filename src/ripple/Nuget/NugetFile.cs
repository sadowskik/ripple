using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FubuCore;
using FubuCore.Descriptions;
using NuGet;
using ripple.Model;

namespace ripple.Nuget
{
	public class NugetName
	{
		public string Name { get; private set; }
		public SemanticVersion Version { get; private set; }

		public static NugetName Parse(string input)
		{
			var index = 0;
			var periodFound = false;

			for (var i = 0; i < input.Length; i++)
			{
				var character = input[i];
				if (character == '.')
				{
					periodFound = true;
					continue;
				}

                if (char.IsLetter(character))
                {
                    periodFound = false;
                    continue;
                }

				if (periodFound && char.IsNumber(character))
				{
					index = i;
					break;
				}
			}

			if (index == 0)
			{
				throw new InvalidOperationException("Invalid package name");
			}

			return new NugetName
			{
				Name = input.Substring(0, index - 1),
				Version = SemanticVersion.Parse(input.Substring(index))
			};
		}
	}

    public class NugetFile : INugetFile, DescribesItself
    {
        private readonly string _path;
	    private readonly SolutionMode _mode;

	    public NugetFile(string path, SolutionMode mode)
        {
            _path = path;
	        _mode = mode;

	        var file = Path.GetFileNameWithoutExtension(path);
	        var result = NugetName.Parse(file);

	        Name = result.Name;
	        Version = result.Version;
	        IsPreRelease = Version.SpecialVersion.IsNotEmpty();
        }

		public string FileName { get { return _path; } }
	    public string Name { get; private set; }
        public SemanticVersion Version { get; private set; }
        public bool IsPreRelease { get; private set; }

        public override string ToString()
        {
            return string.Format("Name: {0}, Version: {1}, IsPreRelease: {2}", Name, Version, IsPreRelease);
        }

		public virtual string ExplodedDirectory(string directory)
		{
			if(_mode == SolutionMode.Classic)
				return directory.AppendPath(Name + "." + Version);

			return directory.AppendPath(Name).ToFullPath();
		}

		public string NugetFolder(Solution solution)
		{
			return ExplodedDirectory(solution.PackagesDirectory());
		}

        public IPackage ExplodeTo(string directory)
        {
	        var explodedDirectory = ExplodedDirectory(directory);

			RippleLog.Info("Exploding to " + explodedDirectory);

            var fileSystem = new FileSystem();
            fileSystem.CreateDirectory(explodedDirectory);
            fileSystem.CleanDirectory(explodedDirectory);

            var package = new ZipPackage(_path);

            package.GetFiles().Each(file => {
                var target = explodedDirectory.AppendPath(file.Path);
                fileSystem.CreateDirectory(target.ParentDirectory());
                
                using (var stream = new FileStream(target, FileMode.Create, FileAccess.Write))
                {
                    file.GetStream().CopyTo(stream);
                }
            });

            fileSystem.CopyToDirectory(_path, explodedDirectory);

			fileSystem.DeleteFile(_path);

	        var newFile = Path.Combine(explodedDirectory, Path.GetFileName(_path));
			return new ZipPackage(newFile);
        }

	    public INugetFile CopyTo(string directory)
	    {
		    var target = Path.Combine(directory, Path.GetFileName(_path));
			new FileSystem().Copy(_path, target);

		    return new NugetFile(target, _mode);
	    }


	    public void Describe(Description description)
	    {
		    description.Title = Name;
		    description.ShortDescription = "Version: {0}, IsPreRelease: {1}".ToFormat(Version, IsPreRelease);
	    }
    }
}