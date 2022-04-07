using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ToDo.Shared.Models;
using TodoBlazor.FuncApi.Models;
using TodoBlazor.FuncApi.Helpers;

namespace TodoBlazor.FuncApi
{
    public static class ToDoApi
    {
        [FunctionName("Create")]
        public static async Task<IActionResult> Create(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post",  Route = "api/todo")] HttpRequest req,
            [Table("todoitems", Connection = "AzureWebJobsStorage")] IAsyncCollector<ItemTableEntity> todoTable,
            ILogger log)
        {
            log.LogInformation("Create new todo item");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var createTodo = JsonConvert.DeserializeObject<CreateItemDto>(requestBody);

            if (createTodo is null || string.IsNullOrWhiteSpace(createTodo.Text)) return new BadRequestResult();

            var item = new Item { Text = createTodo.Text };

            await todoTable.AddAsync(item.ToTableEntity());

            return new OkObjectResult(item);
        }
    }
}
