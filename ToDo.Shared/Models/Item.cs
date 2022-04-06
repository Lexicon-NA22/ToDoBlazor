namespace ToDo.Shared.Models
{
    public class Item
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Completed { get; set; }

        public Item()
        {
            Id = Guid.NewGuid().ToString("n");
            Text = "";
        }
    }
}
