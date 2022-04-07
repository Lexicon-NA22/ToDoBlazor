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
using Microsoft.Azure.Cosmos.Table;
using System.Linq;
using Microsoft.Azure.Cosmos.Table.Queryable;
using Microsoft.Azure.Storage.Blob;

namespace TodoBlazor.FuncApi
{
    public static class ToDoApi
    {
        [FunctionName("Create")]
        public static async Task<IActionResult> Create(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo")] HttpRequest req,
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

        [FunctionName("Get")]
        public static async Task<IActionResult> Get(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todo")] HttpRequest req,
            [Table("todoitems", Connection = "AzureWebJobsStorage")] CloudTable table,
            ILogger log)
        {
            log.LogInformation("Get all items");

            var query = new TableQuery<ItemTableEntity>();
            var res = await table.ExecuteQuerySegmentedAsync(query, null);

            var response = res.Select(Mapper.ToItem).ToList();

            return new OkObjectResult(response);
        }

        [FunctionName("Put")]
        public static async Task<IActionResult> Put(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todo/{id}")] HttpRequest req,
            [Table("todoitems", Connection = "AzureWebJobsStorage")] CloudTable table,
            string id,
            ILogger log)
        {
            log.LogInformation("Update item");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var itemToUpdate = JsonConvert.DeserializeObject<Item>(requestBody);

            if (itemToUpdate is null || itemToUpdate.Id != id) return new BadRequestResult();

            var itemEntity = itemToUpdate.ToTableEntity();
            itemEntity.ETag = "*";

            var operation = TableOperation.Replace(itemEntity);
            await table.ExecuteAsync(operation);

            return new NoContentResult();
        }

        [FunctionName("Delete")]
        public static async Task<IActionResult> Delete(
         [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todo/{id}")] HttpRequest req,
         [Table("todoitems", "Todo", "{id}", Connection = "AzureWebJobsStorage")] ItemTableEntity itemTableToDelete,
         [Table("todoitems", Connection = "AzureWebJobsStorage")] CloudTable todoTable,
         string id,
         ILogger log)
        {
            log.LogInformation("Delete Todo item");

            if (itemTableToDelete is null) return new BadRequestResult();

            // if (string.IsNullOrWhiteSpace(id)) return new BadRequestResult();


            //var itemTableToDelete = new ItemTableEntity
            //{
            //    PartitionKey = "Todo",
            //    RowKey = id,
            //    ETag = "*"
            //};

            var operation = TableOperation.Delete(itemTableToDelete);
            var res = await todoTable.ExecuteAsync(operation);

            return new NoContentResult();
        }

        [FunctionName("RemoveCompleted")]
        public static async Task RemoveCompleted(
           [TimerTrigger("0 */1 * * * *")] TimerInfo timer,
           [Table("todoitems", Connection = "AzureWebJobsStorage")] CloudTable todoTable,
           [Queue("todoqueue", Connection = "AzureWebJobsStorage")] IAsyncCollector<Item> queueItem,
           ILogger log)
        {
            log.LogInformation("Removecompleted started...");

            var query = todoTable.CreateQuery<ItemTableEntity>().Where(i => i.Completed == true).AsTableQuery();
            //var query2 = new TableQuery<ItemTableEntity>().Where(TableQuery.GenerateFilterConditionForBool("Completed", QueryComparisons.Equal, true));

            var result = await todoTable.ExecuteQuerySegmentedAsync(query, null);

            foreach (var item in result)
            {
                await queueItem.AddAsync(item.ToItem());
                await todoTable.ExecuteAsync(TableOperation.Delete(item));

            }
        }

        [FunctionName("GetRemovedFromQueue")]
        public static async Task GetRemovedFromQueue(
          [QueueTrigger("todoqueue", Connection = "AzureWebJobsStorage")] Item item,
          [Blob("done", Connection = "AzureWebJobsStorage")] CloudBlobContainer blobContainer,
          ILogger log)
        {
            log.LogInformation("Queue trigger started...");

            await blobContainer.CreateIfNotExistsAsync();
            var blob = blobContainer.GetBlockBlobReference($"{item.Id}.txt");
            await blob.UploadTextAsync($"{item.Text} is completed");
        }

    }
}
