using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;
using System.Reflection;

namespace ivy.devconsole
{
    public class Devconsole : MonoBehaviour
    {
        static Devconsole Instance;

        public static readonly Color errorColor = new Color(1, .3f, .4f);
        public static readonly Color warningColor = new Color(.9f, .7f, .4f);
        public static readonly Color successColor = new Color(.3f, 1, .4f);
        public static readonly Color infoColor = new Color(.3f, .5f, 1f);
        public static readonly Color commandColor = new Color(1, 1, 1);
        public static readonly Color outputColor = new Color(.5f, .5f, .5f);

        public bool startActive;

        GameObject consoleCanvas;
        GameObject consoleText;
        GameObject previousSelection;
        Transform consoleContent;
        ScrollRect contentScroll;
        TMP_InputField input;

        Action openAction;
        Action closeAction;

        Dictionary<string, MethodInfo> commands = new Dictionary<string, MethodInfo>();
        List<MethodInfo> completions;
        List<string> commandHistory = new List<string>();
        int commandIndex;
        int bufferHeight;
        bool startOfOutput;

        public bool IsOpen { get => consoleCanvas.gameObject.activeSelf; }

        private void Awake()
        {
            if (GameObject.Find("EventSystem") == null) Instantiate(Resources.Load<GameObject>("EventSystem"));
            if (Instance != null)
            {
                Debug.LogError("You have more than one Devconsole in the scene");
                Destroy(this);
            }
            else Instance = this;

            consoleCanvas = Instantiate(Resources.Load<GameObject>("consolecanvas"), this.transform);
            input = consoleCanvas.GetComponentInChildren<TMP_InputField>();
            consoleContent = consoleCanvas.GetComponentInChildren<ScrollRect>().content;
            contentScroll = consoleCanvas.GetComponentInChildren<ScrollRect>();
            consoleText = Resources.Load<GameObject>("consoleText");
            var devinput = consoleCanvas.GetComponentInChildren<DevConsoleInput>();
            devinput.submitHandler = HandleCommand;
            devinput.completionProvider = CompletionSuggestions;
            devinput.suggestionSelectionHandler = SuggestionSelected;
            consoleCanvas.SetActive(startActive);

            var methods = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm => asm.GetTypes())
                .SelectMany(type => type.GetMethods());

            commands = methods
                .Where(method => method.GetCustomAttributes().OfType<ConsoleCommandAttribute>().Any())
                .ToDictionary(m => m.GetCustomAttribute<ConsoleCommandAttribute>().Command);

            completions = methods
                .Where(method => method.GetCustomAttributes().OfType<CompletionProviderAttribute>().Any())
                .ToList();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) && IsOpen && (commandIndex - 1) >= 0)
            {
                commandIndex--;
                input.text = commandHistory[commandIndex];
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) && IsOpen && (commandIndex + 1) < commandHistory.Count)
            {
                commandIndex++;
                input.text = commandHistory[commandIndex];
            }

            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                if (!IsOpen)
                {
                    Open();
                }
                else
                {
                    Close();
                }
            }
        }
        public static void Open()
        {
            Instance.consoleCanvas.gameObject.SetActive(true);
            Instance.previousSelection = EventSystem.current.currentSelectedGameObject;
            if (Instance.previousSelection == Instance.input) Instance.previousSelection = null;
            Instance.openAction?.Invoke();
            Instance.input.text = "";
            Instance.input.Select();
        }

        public static void Close()
        {
            Instance.consoleCanvas.gameObject.SetActive(false);
            Instance.closeAction?.Invoke();
            EventSystem.current.SetSelectedGameObject(Instance.previousSelection);
        }

        public void OnConsoleOpen(Action a) => openAction = a;
        public void OnConsoleClose(Action a) => closeAction = a;

        public static List<string> ParseArgumetns(string command)
        {
            var args = new List<string>();

            bool inString = false;
            string currentCommand = "";
            command += ' '; // add a trailing space so the last word gets picked up as a command
            for (int i = 0; i < command.Length; i++)
            {
                var character = command[i];

                if (i < command.Length - 1 && character == '\\' && command[i + 1] == '"')
                {
                    currentCommand += '"';
                    i++;
                    continue;
                }

                if (character == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (character == ' ' && !inString)
                {
                    if (currentCommand == "") continue;
                    args.Add(currentCommand);
                    currentCommand = "";
                    continue;
                }
                currentCommand += character;
            }
            return args;
        }

        public static string GetArgumentValue(string command)
        {
            if (command.Length == 0 || command[command.Length - 1] == ' ') return "";
            var args = ParseArgumetns(command);
            return args[args.Count - 1];
        }

        public static string GetArgumentName(string command)
        {
            var args = ParseArgumetns(command);
            var count = args.Count - 1;
            if (command.Length > 0 && command[command.Length - 1] == ' ') count++;
            if (count < 1) return "Command";
            if (!Instance.commands.ContainsKey(args[0])) return "invalid command";
            var method = Instance.commands[args[0]];
            var arguments = method.GetParameters();

            if (arguments.Length < count) return "too many arguments";
            return arguments[count - 1].Name;

        }

        void HandleCommand(string command)
        {
            commandHistory.Add(command);
            commandIndex = commandHistory.Count - 1;
            var args = ParseArgumetns(command);
            var mainCommand = args[0];
            args.RemoveAt(0);

            if (!commands.ContainsKey(mainCommand))
            {
                PushMessage("Unknown command: " + mainCommand, errorColor);
                return;
            }

            PushMessage(command, commandColor);

            var method = commands[mainCommand];
            var methodArgs = method.GetParameters();

            if (methodArgs.Length == 0)
            {
                InvokeMethod(method, new object[] { });
                return;
            }

            if (methodArgs[0].ParameterType == typeof(string[]))
            {
                InvokeMethod(method, new object[] { args.ToArray() });
                return;
            }

            if (methodArgs.Where(a => !a.IsOptional).Count() > args.Count)
            {
                PushMessage("not enough arguments supplied to command: " + command, errorColor);
                return;
            }

            int argIndex = 0;
            var dynamicArgs = new List<object>();
            foreach (var param in methodArgs)
            {
                var paramType = param.ParameterType;
                if (args.Count <= argIndex)
                {
                    dynamicArgs.Add(param.DefaultValue);
                }
                else
                {
                    var arg = args[argIndex];
                    if (paramType.IsEnum) dynamicArgs.Add(Enum.Parse(paramType, arg));
                    if (paramType == typeof(string)) dynamicArgs.Add(arg);
                    if (paramType == typeof(int)) dynamicArgs.Add(int.Parse(arg));
                    if (paramType == typeof(float)) dynamicArgs.Add(float.Parse(arg));
                }
                argIndex++;
            }
            InvokeMethod(method, dynamicArgs.ToArray());
        }

        void InvokeMethod(MethodInfo methodInfo, object[] args)
        {
            var returnValue = methodInfo.Invoke(null, args);
            if (methodInfo.ReturnType == typeof(IEnumerator)) StartCoroutine((IEnumerator)returnValue);
            else if (methodInfo.ReturnType != typeof(void)) PushMessage(returnValue.ToString());
        }

        #region completion suggestions
        void SuggestionSelected(string param)
        {
            if (param.Contains(" ")) param = $"\"{param}\"";
            var arguments = ParseArgumetns(input.text);
            var text = input.text;
            if (text.Length > 0 && text[text.Length - 1] == ' ')
            {
                input.text += param;
                return;
            }
            arguments[arguments.Count - 1] = param;
            input.text = string.Join(" ", arguments);
        }

        string[] CompletionSuggestions(string command)
        {
            var arguments = ParseArgumetns(command);

            if (arguments.Count == 0) return null;

            var argumentCount = arguments.Count - 1;
            if (command[command.Length - 1] == ' ') argumentCount++;

            if (argumentCount == 0) return commands.Keys.ToArray();

            if (!commands.ContainsKey(arguments[0])) return null;

            var method = commands[arguments[0]];
            var parameters = method.GetParameters();

            if (parameters.Length <= argumentCount - 1) return null;

            var param = parameters[argumentCount - 1];
            var paramType = param.ParameterType;

            if (paramType.IsEnum)
            {
                var enums = new List<string>();
                foreach (var e in Enum.GetValues(paramType))
                {
                    enums.Add(e.ToString());
                }
                return enums.ToArray();
            }

            // find a suitable completion provider
            var provider = completions.Where(m =>
            {
                var completionAtt = m.GetCustomAttribute<CompletionProviderAttribute>();
                return (completionAtt.commandName == null || completionAtt.commandName == arguments[0]) &&
                       (completionAtt.paramaterName == null || completionAtt.paramaterName == param.Name) &&
                       (completionAtt.paramaterType == null || completionAtt.paramaterType == paramType);
            })
            .OrderByDescending(m => m.GetCustomAttribute<CompletionProviderAttribute>().priority)
            .FirstOrDefault();
            return (string[])provider?.Invoke(null, new object[] { });
        }
        #endregion

        #region console messages
        public static void PushMessage(string message, float fontSize = 0) => PushMessage(message, outputColor, fontSize);
        public static void PushMessage(string message, Color color, float fontSize = 0)
        {
            if (color == commandColor)
            {
                message = $"► {message}";
                Instance.startOfOutput = true;
            }
            else
            {
                if (Instance.startOfOutput) message = $"◄ {message}";
                else message = $"   {message}";
                Instance.startOfOutput = false;
            }

            var messageInstance = InstantiateInConsole(Instance.consoleText);
            var t = messageInstance.GetComponent<TextMeshProUGUI>();
            t.text = message;
            if (fontSize > 0) t.fontSize = fontSize;
            t.color = color;
            var rect = messageInstance.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, t.preferredHeight);
            PushComponent(messageInstance);
        }

        public static GameObject InstantiateInConsole(GameObject prefab) => Instantiate(prefab, Instance.consoleContent);
        public static void PushComponent(GameObject component)
        {
            var rect = component.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(10, -Instance.bufferHeight);
            Instance.bufferHeight += (int)rect.sizeDelta.y + 10;

            var contentRect = Instance.consoleContent.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, Instance.bufferHeight + 30);
            Instance.contentScroll.verticalNormalizedPosition = 0;
        }
        #endregion

        #region Debug.Log support
        private void OnEnable() => Application.logMessageReceived += HandleLog;
        private void OnDisable() => Application.logMessageReceived -= HandleLog;

        void HandleLog(string condition, string stackTrace, LogType type)
        {
            var c = outputColor;
            if (type == LogType.Error || type == LogType.Exception) c = errorColor;
            if (type == LogType.Warning) c = warningColor;
            if (type == LogType.Assert) c = infoColor;
            PushMessage(condition, c);
            //PushMessage(stackTrace, fontSize:14);
        }
        #endregion

        #region stock commands
        [CompletionProvider(paramaterName: "commandName", paramaterType: typeof(string))]
        public static string[] ListCommands() => Instance.commands.Keys.ToArray();

        [ConsoleCommand("help", description: "hi :3")]
        public static void Help(string commandName = "")
        {
            if (commandName == "")
            {
                PushMessage("Type help <commandname> for command specific help.");
                PushMessage("List of available commands:");

                foreach (var command in Instance.commands.Keys) PushMessage("    " + command);
                return;
            }

            if (!Instance.commands.ContainsKey(commandName))
            {
                PushMessage($"Command not found: {commandName}", errorColor);
                return;
            }

            var method = Instance.commands[commandName];
            var cmd = method.GetCustomAttribute<ConsoleCommandAttribute>();
            var parameters = method.GetParameters();

            if (cmd.Description == null || cmd.Description.Length == 0) PushMessage(commandName);
            else PushMessage($"{commandName} - {cmd.Description}");

            var types = string.Join(", ", parameters.Select(p => DisplayType(p.ParameterType)).ToArray());
            PushMessage($"{commandName} ::  ({types}) -> {DisplayType(method.ReturnType)}");

            if (parameters.Count() == 0) PushMessage($"This command has no arguments, call it by simply typing: {commandName}");
            else PushMessage($"{commandName} {string.Join(" ", parameters.Select(DisplayParameter).ToArray())}");

            if (cmd.Example != null)
            {
                PushMessage("example usage:");
                PushMessage(cmd.Example, infoColor);
            }
        }
        static string DisplayType(Type type) => type.ToString().Replace("System.", "");
        static string DisplayParameter(ParameterInfo p)
        {
            if (p.HasDefaultValue && p.ParameterType == typeof(string)) return $"{p.Name}=\"{p.DefaultValue}\"";
            if (p.HasDefaultValue) return $"{p.Name}={p.DefaultValue}";
            return p.Name;
        }

        [ConsoleCommand("clear", description: "clears the screen")]
        public static void Clear()
        {
            Instance.commandHistory.Clear();
            foreach (Transform child in Instance.consoleContent.transform) Destroy(child.gameObject);

            Instance.bufferHeight = 0;
            var r = Instance.consoleContent.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(r.sizeDelta.x, (Instance.bufferHeight + 1) * 30);
            Instance.contentScroll.verticalNormalizedPosition = 0;
        }
    }
    #endregion

    #region properties
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class ConsoleCommandAttribute : Attribute
    {
        public readonly string Command;
        public readonly string Description;
        public readonly string Example;

        public ConsoleCommandAttribute(string command, string description = null, string example = null)
        {
            this.Command = command;
            this.Description = description;
            this.Example = example;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class CompletionProviderAttribute : Attribute
    {
        public readonly string commandName;
        public readonly string paramaterName;
        public readonly Type paramaterType;

        public int priority
        {
            get
            {
                if (priorityOverride != 0) return priorityOverride;
                var score = 0;
                if (commandName != null) score += 4;
                if (paramaterName != null) score += 2;
                if (paramaterType != null) score += 1;
                return score;
            }
        }

        readonly int priorityOverride;

        public CompletionProviderAttribute(string commandName = null, string paramaterName = null, Type paramaterType = null, int priorityOverride = 0)
        {
            this.commandName = commandName;
            this.paramaterName = paramaterName;
            this.paramaterType = paramaterType;
            this.priorityOverride = priorityOverride;
        }
    }
    #endregion
}

