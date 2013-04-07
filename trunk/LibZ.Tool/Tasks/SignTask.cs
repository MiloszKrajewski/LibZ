using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class SignTask: TaskBase
	{
		internal void Execute(string keyFileName, bool force, string[] patterns)
		{
			foreach (var fileName in FindFiles(patterns))
			{
				var assembly = AssemblyDefinition.ReadAssembly(fileName);
			}
		}
	}
}
