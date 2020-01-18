![](./demo.gif)

A runtime developer console for unity.

Features:
- Reflection based api for adding custom commands
- Autocompletion and ability to add custom completion handlers
- Small codebase of `~600` loc
- Console open/close handlers
- Command history navigation
- Integrates with native logging (`Debug.Log`)
- Custom UI component embedding

# Installation and usage

Install the unity package and add the `Devconsole` script to any object in the scene.

Pressing the backtick key \` should toggle the console, type `help` to get started.

When autocomplete suggestions pop up you can navigate them by pressing `tab`. You can toggle the autocomplete on/off by pressing `F1`.

## Adding custom commands

Custom commands are just static functions with a `ConsoleCommand` attribute from the `ivy.devconsole` namespace, look at `TestCommands.cs` for some examples.

```C#
[ConsoleCommand("add", description: "add two numbers together", example: "add 10 20")]
public static int add(int a, int b) => a + b;
```

The supported parameter types are `int`, `string`, `enum` and `float`, optional arguments of these types are also supported.

You can also parse your arguments manually by having `string[] args` be the only function parameter.

If the return type of your function is *not* `void`, the return value will automatically be logged to the console.

If you specify the return type of your function as `IEnumerator` it will be executed as a coroutine.

## Adding custom completions

Completions are static functions of `void -> string[]` decorated with the `CompletionProvider` attribute.

```C#
[CompletionProvider(commandName: "echo")]
public static string[] EchoCompletions() => new string[] { "hello world", "hello devconsole", "testing" };
```
You can provide completions based on `commandName`, `paramaterName` and/or `paramaterType`. completion methods are then assigned a priority score depending on how specific they are, and the method with the highest priority is used.
You can override the priority by passing in the `priorityOverride` property to the constructor.

## Open/close handler

Use these handlers to detect when the console changes state.

```C#
var d = FindObjectOfType<Devconsole>();
d.OnConsoleOpen(() => Debug.Log("the console is open"));
d.OnConsoleClose(() => Debug.Log("the console is closed"));
```