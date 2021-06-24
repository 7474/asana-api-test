using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace asana_api_test
{
    public static class DictExt
    {
        public static string Ref<V>(this IDictionary<string, V> dict, string key)
        {
            return dict.ContainsKey(key) ? $"{dict[key]}" : "";
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var pat = Environment.GetEnvironmentVariable("ASANA_API_PAT");
            var workspaceId = Environment.GetEnvironmentVariable("WORKSPACE_ID");
            var teamId = Environment.GetEnvironmentVariable("TEAM_ID");
            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {pat}");
            var client = new asana_oasClient(http);

            var workspaces = (await client.GetWorkspacesAsync(null, null, null, null));
            Console.WriteLine(JsonConvert.SerializeObject(workspaces));
            var workspace = workspaces.Data.First();


            var teams = (await client.GetTeamsForOrganizationAsync(workspace.Gid, null, null, null, null));
            Console.WriteLine(JsonConvert.SerializeObject(teams));


            // https://developers.asana.com/explorer
            // https://developers.asana.com/docs/standard-rate-limits
            // Free 150/minute
            // Premium 1500/minute
            var projects = (await client.GetProjectsAsync(null, null, workspaceId, teamId, null, null, new string[] {
                "name", "created_at", "modified_at", "due_on","start_on"
            }))
                .Data.Select(p =>
               {
                   // ページネートは取り合えずさぼった。
                   var tasks = Task.Run(() => client.GetTasksForProjectAsync(p.Gid, null, new string[] {
                       "name", "created_at", "modified_at", "completed_at", "due_on","start_on"
                   }, 100, null)).Result.Data;
                   Console.WriteLine(JsonConvert.SerializeObject(p));
                   Console.WriteLine(JsonConvert.SerializeObject(tasks));
                   return new
                   {
                       project = p,
                       tasks = tasks,
                       // プロジェクトが閉じる前に完了していたタスクの数
                       completedTaskCount = tasks.Where(t =>
                            (t.AdditionalProperties.Ref("completed_at") + "x")
                            // アーカイブ日時がない気がする
                              .CompareTo(p.AdditionalProperties.Ref("modified_at")) <= 0
                           ).Count(),
                   };
               }).ToList();

            Console.WriteLine(JsonConvert.SerializeObject(projects, Formatting.Indented));
            foreach (var p in projects)
            {
                Console.WriteLine($"\"{p.project.Name}\",\"{p.project.AdditionalProperties.Ref("modified_at")}\",{p.completedTaskCount},{p.tasks.Count}");
            }

            return;
        }
    }
}
