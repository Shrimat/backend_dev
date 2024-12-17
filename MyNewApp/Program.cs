using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());

var app = builder.Build();

// Middleware URL rewrite, middlewares runs in order
app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));
// Custom middleware, prints information of HTTP request to console
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

// Client is requesting data from the server
// app.MapGet("/", () => "Hello World!");
var todos = new List<Todo>();

// Getting all the todos in our list
app.MapGet("/todos/", (ITaskService service) => service.GetTodos());
// Getting the todo in our list with t.Id == id
app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITaskService service) =>
{
    var targetTodo = service.GetTodoById(id);
    return targetTodo is null ? TypedResults.NotFound() : TypedResults.Ok(targetTodo);
});
// Adding a todo to our list
app.MapPost("/todos", (Todo task, ITaskService service) =>
{
    service.AddTodo(task);
    return TypedResults.Created("/todos/{id}", task);
})
// Endpoint filter to find errors in argument to post request
.AddEndpointFilter(async (context, next) =>
{
    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();
    if (taskArgument.DueDate < DateTime.UtcNow)
    {
        errors.Add(nameof(Todo.DueDate), ["Cannot have due date in the past."]);
    }
    if (taskArgument.isCompleted)
    {
        errors.Add(nameof(Todo.isCompleted), ["Cannot add completed todo."]);
    }
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }
    return await next(context);
});
// Deleting a todo from our list
app.MapDelete("/todos/{id}", (int id, ITaskService service) =>
{
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});

app.Run();

public record Todo(int Id, string Name, DateTime DueDate, bool isCompleted);

// Create a service that replaces logic inline with methods in the service.
interface ITaskService {
    Todo? GetTodoById(int id);
    List<Todo> GetTodos();
    void DeleteTodoById(int id);
    Todo AddTodo(Todo task);
}

class InMemoryTaskService : ITaskService {
    private readonly List<Todo> _todos = [];

    public Todo AddTodo(Todo task) {
        _todos.Add(task);
        return task;
    }

    public void DeleteTodoById(int id) {
        _todos.RemoveAll(task => id == task.Id);
    }

    public Todo? GetTodoById(int id) {
        return _todos.SingleOrDefault(t => id == t.Id);
    }

    public List<Todo> GetTodos() {
        return _todos;
    }

}