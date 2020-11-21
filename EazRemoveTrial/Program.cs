using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Console = Colorful.Console;

namespace MaddoxXSoHappy
{
    internal class Program
    {
        private static String Filepath = null;

        private static Int32 TypeIndex = -1;

        private static void Main(String[] args)

        {
            Console.Title = "EazRemoveTrial";
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: EazUnlock <file> [index]");
                Environment.Exit(1);
            }

            Filepath = args[0];

            if (args.Length > 1)
            {
                if (!Int32.TryParse(args[1], out TypeIndex) || TypeIndex < 0)
                {
                    Console.WriteLine("Index Must Be A Non-Negative Integer, Using Default Of 0");
                    TypeIndex = 0;
                }
            }

            Fix(Filepath);
        }

        private static void Fix(String filepath)
        {
            Fix(ModuleDefMD.Load(filepath));
        }

        private static void Fix(ModuleDefMD module)
        {
            IList<TypeDef> evalTypes = FindEvaluationTypes(module);
            if (evalTypes.Count == 0)
            {
                Console.WriteLine("Module Does Not Seem Limited By Eazfuscator.NET Evaluation", Color.Green);
            }
            else if (evalTypes.Count > 1)
            {
                Console.WriteLine("Multiple Evaluation-Like Types Detected : ");
                foreach (var evalType in evalTypes)
                    Console.WriteLine(" {0} (MDToken = 0x{1:X8})", evalType.FullName, evalType.MDToken.Raw);

                if (TypeIndex < 0)
                {
                    TypeIndex = 0;
                }
                if (TypeIndex >= evalTypes.Count)
                {
                    Console.WriteLine("Type Index {0} Out-Of-Range, Using Default Of 0", TypeIndex);
                    TypeIndex = 0;
                }

                Console.WriteLine("Patching Type At Index {0}", TypeIndex);
                Patch(evalTypes[TypeIndex]);
                Write(module);
            }
            else
            {
                Console.WriteLine("Evaluation Type Found : (MDToken = 0x{1:X8})", Color.Green,
                evalTypes[0].FullName, evalTypes[0].MDToken.Raw);
                Patch(evalTypes[0]);
                Write(module);
            }
        }

        private static void Patch(TypeDef evalType)
        {
            var badMethod = GetStaticMethods(evalType, "System.Boolean", "System.Boolean")[0];
            var instructions = badMethod.Body.Instructions;
            instructions.Clear();

            instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
            instructions.Add(OpCodes.Ret.ToInstruction());

            badMethod.Body.ExceptionHandlers.Clear();
        }

        private static IList<TypeDef> FindEvaluationTypes(ModuleDefMD module)
        {
            var evalTypes = new List<TypeDef>();

            var types = module.GetTypes();
            foreach (var typeDef in types)
            {
                if (typeDef.Methods.Count == 4
                && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1
                && CountStaticMethods(typeDef, "System.Void") == 2
                && CountStaticMethods(typeDef, "System.Boolean") == 1)
                    evalTypes.Add(typeDef);
                else if (typeDef.Methods.Count == 3
                && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1
                && CountStaticMethods(typeDef, "System.Void") == 1
                && CountStaticMethods(typeDef, "System.Boolean") == 1)
                    evalTypes.Add(typeDef);
            }

            return evalTypes;
        }

        public static Int32 CountStaticMethods(TypeDef def, String retType, params String[] paramTypes)
        {
            return GetStaticMethods(def, retType, paramTypes).Count;
        }

        public static IList<MethodDef> GetStaticMethods(TypeDef def, String retType, params String[] paramTypes)
        {
            List<MethodDef> methods = new List<MethodDef>();

            if (!def.HasMethods)
                return methods;

            foreach (var method in def.Methods)
            {
                if (!method.IsStatic)
                    continue;

                if (!method.ReturnType.FullName.Equals(retType))
                    continue;

                if (paramTypes.Length != method.Parameters.Count)
                    continue;

                Boolean paramsMatch = true;
                for (Int32 i = 0; i < paramTypes.Length && i < method.Parameters.Count; i++)
                {
                    if (!method.Parameters[i].Type.FullName.Equals(paramTypes[i]))
                    {
                        paramsMatch = false;
                        break;
                    }
                }

                if (!paramsMatch)
                    continue;

                methods.Add(method);
            }

            return methods;
        }

        private static void Write(ModuleDefMD module)
        {
            Console.WriteLine("");
            String outputPath = GetOutputFilepath();
            Console.WriteLine("Saving {0}", outputPath, Color.Green);

            var options = new ModuleWriterOptions(module);
            options.MetadataOptions.Flags |= MetaDataFlags.PreserveAll;
            options.MetadataOptions.Flags |= MetaDataFlags.KeepOldMaxStack;
            module.Write(outputPath, options);
            Console.ReadKey();
        }

        private static String GetOutputFilepath()
        {
            String dir = Path.GetDirectoryName(Filepath);
            String noExt = Path.GetFileNameWithoutExtension(Filepath);
            String ext = Path.GetExtension(Filepath);
            String newFilename = String.Format("{0}-Removed{1}", noExt, ext);
            return Path.Combine(dir, newFilename);
        }
    }

    internal class MetaDataFlags
    {
        internal static MetadataFlags KeepOldMaxStack;
        internal static MetadataFlags PreserveAll;
    }
}