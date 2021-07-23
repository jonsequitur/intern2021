﻿using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Notebook;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Extension
{
    public static class KernelExtensions
    {
        public static T UseProgressiveLearning<T>(this T kernel, Lesson lesson) where T : Kernel
        {
            kernel.UseProgressiveLearningMiddleware<T>(lesson);
            return kernel;
        }

        public static T UseProgressiveLearning<T>(this T kernel) where T : Kernel, IKernelCommandHandler<ParseNotebook>
        {
            var argument = new Argument<FileInfo>("file");
            
            var startCommand = new Command("#!start-lesson")
            {
                argument
            };

            startCommand.Handler = CommandHandler.Create<FileInfo, KernelInvocationContext>(async (file, context) =>
            {
                var rawData = await File.ReadAllBytesAsync(file.FullName);
                // todo: NotebookFileFormatHandler.Parse what are its last two arguments
                var document = NotebookFileFormatHandler.Parse(file.Name, rawData, "csharp", new Dictionary<string, string>());
                NotebookLessonParser.Parse(document, out var lessonBlueprint, out var challengeBlueprints);
                var challenges = challengeBlueprints.Select(b => b.ToChallenge());
                challenges.SetDefaultProgression();
                var lesson = lessonBlueprint.ToLesson();
                lesson.SetChallengeLookup(name =>
                {
                    return challenges.FirstOrDefault(c => c.Name == name);
                });

                await InitializeLesson(lesson);

                await lesson.StartChallengeAsync(challenges.First());

                await Bootstrapping(lesson);

                await InitializeChallenge(lesson.CurrentChallenge);

                kernel.UseProgressiveLearningMiddleware<T>(lesson);
            });

            kernel.AddDirective(startCommand);
            return kernel;
        }

        private static T UseProgressiveLearningMiddleware<T>(this T kernel, Lesson lesson) where T : Kernel
        {
            kernel.AddMiddleware(async (command, context, next) =>
            {
                switch (command)
                {
                    case SubmitCode submitCode:
                        if (lesson.IsSetupCommand(submitCode))
                        {
                            await next(command, context);
                            break;
                        }
                        var currentChallenge = lesson.CurrentChallenge;

                        await next(command, context);

                        var events = context.KernelEvents.ToSubscribedList();
                        await lesson.CurrentChallenge.Evaluate(submitCode.Code, events);
                        var view = currentChallenge.CurrentEvaluation.FormatAsHtml();
                        var formattedValues = FormattedValue.FromObject(view);
                        context.Publish(
                            new DisplayedValueProduced(
                                view,
                                command,
                                formattedValues));

                        if (lesson.CurrentChallenge != currentChallenge)
                        {
                            await InitializeChallenge(lesson.CurrentChallenge);
                        }

                        break;
                    default:
                        await next(command, context);
                        break;
                }
            });

            return kernel;
        }

        private static async Task InitializeChallenge(Challenge challengeToInit)
        {
            if (challengeToInit is null)
            {
                return;
            }

            if (!challengeToInit.IsSetup)
            {
                foreach (var setup in challengeToInit.Setup)
                {
                    await Kernel.Root.SendAsync(setup);
                }
                challengeToInit.IsSetup = true;
            }

            foreach (var content in challengeToInit.Contents)
            {
                await Kernel.Root.SendAsync(content);
            }
            foreach (var setup in challengeToInit.EnvironmentSetup)
            {
                await Kernel.Root.SendAsync(setup);
            }
        }

        private static async Task Bootstrapping(Lesson lesson)
        {
            var kernel = Kernel.Root.FindKernel("csharp");
            await kernel.SubmitCodeAsync($"#r \"{typeof(Lesson).Assembly.Location}\"");
            await kernel.SubmitCodeAsync($"#r \"{typeof(Lesson).Namespace}\"");
            if (kernel is DotNetKernel dotNetKernel)
            {
                await dotNetKernel.SetVariableAsync<Lesson>("Lesson", lesson);
            }
        }

        private static async Task InitializeLesson(Lesson lesson)
        {
            foreach (var setup in lesson.Setup)
            {
                await Kernel.Root.SendAsync(setup);
            }
        }
    }
}
