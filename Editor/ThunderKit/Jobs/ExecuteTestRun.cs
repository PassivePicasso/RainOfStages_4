using RoR2;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using ThunderKit.Core.Pipelines.Jobs;

namespace LocalDevelopment.Scripts
{
    [PipelineSupport(typeof(Pipeline))]
    public class ExecuteTestRun : ExecuteProcess
    {
        public enum SurvivorBody { CommandoBody, EngiBody, Bandit2Body, CaptainBody, CrocoBody, HereticBody, HuntressBody, LoaderBody, MageBody, MercBody, ToolbotBody, TreebotBody }
        public SurvivorBody body;
        public Run run;
        public override Task Execute(Pipeline pipeline)
        {
            var args = new StringBuilder();
            for (int i = 0; i < arguments.Length; i++)
            {
                args.Append(arguments[i].Resolve(pipeline, this));
                args.Append(" ");
            }
            args.Append($"--Survivor={body} ");
            args.Append($"--LoadGameMode={run.name} ");

            var exe = executable.Resolve(pipeline, this);
            var pwd = workingDirectory.Resolve(pipeline, this);
            var startInfo = new ProcessStartInfo(exe)
            {
                WorkingDirectory = pwd,
                Arguments = args.ToString(),
                //Standard output redirection doesn't currently work with bepinex, appears to be considered a bepinex bug
                //RedirectStandardOutput = true,
                UseShellExecute = true
            };

            pipeline.Log(LogLevel.Information, $"Executing {exe} in working directory {pwd}");

            Process.Start(startInfo);
            return Task.CompletedTask;
        }
    }
}
