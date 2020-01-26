using System.Collections;
using UnityEngine;

namespace Ivy.Devconsole
{
    public class TestCommands
    {
        public enum Color { Red, Green, Blue }

        [ConsoleCommand("color", example: "color Red")]
        public static void color(Color col)
        {
            var c = new UnityEngine.Color(1, 0, 0);
            if (col == Color.Green) c = new UnityEngine.Color(0, 1, 0);
            if (col == Color.Blue) c = new UnityEngine.Color(0, 0, 1);
            Devconsole.PushMessage(col.ToString(), c);
        }

        [ConsoleCommand("image", description: "embed an image in the console")]
        public static void imageEmbed()
        {
            var img = Devconsole.InstantiateInConsole(Resources.Load<GameObject>("consoleImage"));
            Devconsole.PushComponent(img);
        }

        [CompletionProvider(commandName: "echo")]
        public static string[] EchoCompletions() => new string[] { "hello world", "hello devconsole", "testing" };

        [ConsoleCommand("echo", description: "show all the provided arguments in the console", example: "echo \"hello world\"")]
        public static void echo(string[] args) => Devconsole.PushMessage(string.Join(" ", args));

        [ConsoleCommand("add", description: "add two numbers together", example: "add 10 20")]
        public static int add(int a, int b) => a + b;

        [ConsoleCommand("hack", description: "coroutine test")]
        public static IEnumerator hack()
        {
            for (int i = 0; i < 5; i++)
            {
                Devconsole.PushMessage("initializing...");
                yield return new WaitForSeconds(.2f);
            }

            for (int i = 0; i < 50; i++)
            {
                Devconsole.PushMessage("hacking...", i % 2 == 0 ? Devconsole.outputColor : Devconsole.commandColor);
                yield return new WaitForSeconds(.05f);
            }

            Devconsole.PushMessage("You've been hacked!", Devconsole.errorColor);
            yield return new WaitForSeconds(.5f);
            Devconsole.PushMessage("(Not really, dont worry)", Devconsole.successColor);
            yield return new WaitForSeconds(2.5f);
            Devconsole.PushMessage("Unless?", Devconsole.errorColor);
            yield return new WaitForSeconds(.5f);
            Devconsole.Clear();
        }
    }
}

