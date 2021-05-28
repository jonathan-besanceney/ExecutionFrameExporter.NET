using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Samples.Debugging.MdbgEngine;

namespace FrameExporter.Utils
{
    public class NextStepMgr
    {
        static NextStepMgr()
        {
            nextSteps = new List<NextStep>();
        }

        public void Add(NextStep nextStep)
        {
            nextSteps.Add(nextStep);
        }

        void Flush()
        {
            nextSteps.Clear();
        }

        public void Run()
        {
            if (nextSteps.Count > 1)
                nextSteps.Sort(NextStep.sortDescending());
            foreach(NextStep nextStep in nextSteps)
            {
                if (nextStep != null) nextStep.Run();
            }
            Flush();
        }
        private static List<NextStep> nextSteps;
    }
    public class NextStep
    {
        public NextStep(MDbgProcess process, MDbgThread thread, StepperType type, bool singleStepInstructions = false)
        {
            m_process = process;
            m_thread = thread;
            m_type = type;
            m_singleStepInstructions = singleStepInstructions;
        }

        public void Run()
        {
            if (m_thread.Suspended) Trace.WriteLine($"Thread[{m_thread.Number}].CorThread.UserState : {m_thread.CorThread.UserState}");
            if (
                m_thread.Suspended 
                && m_thread.CorThread.UserState != Microsoft.Samples.Debugging.CorDebug.NativeApi.CorDebugUserState.USER_UNSAFE_POINT
                )
                m_thread.Suspended = false;
            switch (m_type)
            {
                case StepperType.In:
                    m_process.StepInto(m_thread, m_singleStepInstructions);
                    break;
                case StepperType.Out:
                    m_process.StepOut(m_thread);
                    break;
                case StepperType.Over:
                    m_process.StepOver(m_thread, m_singleStepInstructions);
                    break;
            }
        }

        new public string ToString()
        {
            return $"NextStep: {m_type} on Thread {m_thread.Number}";
        }

        private class sortNextStepDescending : IComparer<NextStep>
        {
            int IComparer<NextStep>.Compare(NextStep x, NextStep y)
            {
                if (x == null && y == null) return 0;
                if (x == null && y != null) return 1;
                if (x != null && y == null) return -1;
                if (x.m_thread.Number == y.m_thread.Number) return 0;
                if (x.m_thread.Number > y.m_thread.Number) return -1;
                else return 1;
            }
        }

        public static IComparer<NextStep> sortDescending()
        {
            return new sortNextStepDescending();
        }

        private MDbgProcess m_process;
        private MDbgThread m_thread;
        private StepperType m_type;
        private bool m_singleStepInstructions;
    }
}
