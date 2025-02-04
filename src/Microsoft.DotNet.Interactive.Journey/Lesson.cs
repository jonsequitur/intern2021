﻿
using Microsoft.DotNet.Interactive.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Interactive.Journey
{
    public enum LessonMode
    {
        TeacherMode,
        StudentMode
    }

    public class Lesson
    {
        public static string Name { get; private set; }
        public static Challenge CurrentChallenge { get; private set; }
        public static IReadOnlyList<SubmitCode> Setup { get; private set; }
        public static LessonMode Mode { get; internal set; } = LessonMode.TeacherMode;

        private static Func<string, Task<Challenge>> _challengeLookup;

        public static Task StartChallengeAsync(Challenge challenge)
        {
            CurrentChallenge = challenge;
            if (CurrentChallenge is not null)
            {
                CurrentChallenge.Revealed = true;
            }

            return Task.CompletedTask;
        }

        public static async Task StartChallengeAsync(string name)
        {
            var challenge = await _challengeLookup(name);
            if (challenge is not null)
            {
                await StartChallengeAsync(challenge);
            }
        }

        public static void SetChallengeLookup(Func<string, Challenge> handler)
        {
            _challengeLookup = name => Task.FromResult(handler(name));
        }

        public static void SetChallengeLookup(Func<string, Task<Challenge>> handler)
        {
            _challengeLookup = handler;
        }

        public static void ResetChallenge()
        {
            switch (Mode)
            {
                case LessonMode.StudentMode:
                    break;
                case LessonMode.TeacherMode:
                    CurrentChallenge = new Challenge();
                    break;
            }
        }

        public static void From(LessonDefinition definition)
        {
            Name = definition.Name;
            Setup = definition.Setup;
        }

        public static bool IsSetupCommand(KernelCommand command)
        {
            return (CurrentChallenge?.EnvironmentSetup?.Any(s => s == command || s == command.Parent) ?? false)
                   || (CurrentChallenge?.Setup?.Any(s => s == command || s == command.Parent) ?? false)
                   || (Setup?.Any(s => s == command || s == command.Parent) ?? false);
        }

        public static void Clear()
        {
            Name = "";
            CurrentChallenge = null;
        }
    }
}
