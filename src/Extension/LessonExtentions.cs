﻿using Microsoft.DotNet.Interactive.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension
{
    public static class LessonExtentions
    {
        public static bool IsSetupCommand(this Lesson lesson,KernelCommand command)
        {
            return lesson.CurrentChallenge.Setup.Any(s => s == command);
        }
    }
}
