using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System.Drawing;
using Console = Colorful.Console;

namespace EazRemoveTrial;

internal class Program
{
    private static string? _filepath;

    private static int _typeIndex = -1;

    private static void Main(string?[] args)
    {
        Console.Title = "EazRemoveTrial";

        if (args.Length < 1)
        {
            Console.Write("Exe Path: ");
            _filepath = Console.ReadLine();
            _filepath = _filepath.Replace("\"", "");
            Console.Write("Index: ");
            var index = Console.ReadLine();
            if (!int.TryParse(index, out _typeIndex) || _typeIndex < 0)
            {
                Console.WriteLine("Index Must Be A Non-Negative Integer, Using Default Of 0");
                Console.WriteLine("");
                _typeIndex = 0;
            }
        }
        else if (args.Length == 1)
        {
            _filepath = args[0];
            _typeIndex = 0;
        }
        else
        {
            _filepath = args[0];

            if (!int.TryParse(args[1], out _typeIndex) || _typeIndex < 0)
            {
                Console.WriteLine("Index Must Be A Non-Negative Integer, Using Default Of 0");
                Console.WriteLine("");
                _typeIndex = 0;
            }
        }

        try
        {
            if (string.IsNullOrWhiteSpace(_filepath) ||
                !Path.GetExtension(_filepath).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                !Path.GetExtension(_filepath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Enter a valid .exe or .dll path.");
            }
        }
        catch
        {
            //
        }
        Fix(ModuleDefMD.Load(_filepath));
    }

    private static void Fix(ModuleDefMD module)
    {
        var evalTypes = FindEvaluationTypes(module);
        if (evalTypes.Count == 0)
        {
            Console.WriteLine("Module Does Not Seem Limited By Eazfuscator.NET Evaluation", Color.Red);
        }
        else if (evalTypes.Count > 1)
        {
            Console.WriteLine("Multiple Evaluation-Like Types Detected : ");
            foreach (var evalType in evalTypes)
                Console.WriteLine($" {evalType.FullName} MDToken = 0x{evalType.MDToken.Raw:X8}");

            if (_typeIndex < 0)
            {
                _typeIndex = 0;
            }
            if (_typeIndex >= evalTypes.Count)
            {
                Console.WriteLine($"Type Index {_typeIndex} Out-Of-Range, Using Default Of 0");
                _typeIndex = 0;
            }

            Console.WriteLine($"Patching Type At Index {_typeIndex}");
            Patch(evalTypes[_typeIndex]);
            Write(module);
        }
        else
        {
            Console.WriteLine($"Evaluation Type Found {evalTypes[0].FullName}: MDToken = 0x{evalTypes[0].MDToken.Raw:X8}", Color.Lime);
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
        return module.GetTypes().Where(typeDef => IsConsole(typeDef) || IsWinForm(typeDef)).ToList();
    }

    private static bool IsConsole(TypeDef typeDef)
    {
        return typeDef.Methods.Count switch
        {
            4 when CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1 &&
                   CountStaticMethods(typeDef, "System.Void") == 2 &&
                   CountStaticMethods(typeDef, "System.Boolean") == 1 => true,
            3 when CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1 &&
                   CountStaticMethods(typeDef, "System.Void") == 1 &&
                   CountStaticMethods(typeDef, "System.Boolean") == 1 => true,
            _ => false
        };
    }

    private static bool IsWinForm(TypeDef typeDef)
    {
        return typeDef.Methods.Count switch
        {
            6 when CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 2 &&
                   CountStaticMethods(typeDef, "System.Void", "System.Threading.ThreadStart") == 1 &&
                   CountStaticMethods(typeDef, "System.Void") == 2 &&
                   CountStaticMethods(typeDef, "System.Boolean") == 1 => true,
            _ => false
        };
    }

    private static int CountStaticMethods(TypeDef def, string retType, params string[] paramTypes)
    {
        return GetStaticMethods(def, retType, paramTypes).Count;
    }

    private static IList<MethodDef> GetStaticMethods(TypeDef def, string retType, params string[] paramTypes)
    {
        var methods = new List<MethodDef>();

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

            var paramsMatch = true;
            for (var i = 0; i < paramTypes.Length && i < method.Parameters.Count; i++)
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
        var outputPath = GetOutputFilepath();
        Console.WriteLine("Saving {0}", outputPath, Color.DodgerBlue);

        var options = new ModuleWriterOptions(module);
        options.MetadataOptions.Flags |= MetaDataFlags.PreserveAll;
        options.MetadataOptions.Flags |= MetaDataFlags.KeepOldMaxStack;
        module.Write(outputPath, options);
        Console.WriteLine("");
        Console.Write("Press any key to exit...");
        Console.ReadKey();
    }

    private static string GetOutputFilepath()
    {
        var dir = Path.GetDirectoryName(_filepath);
        var noExt = Path.GetFileNameWithoutExtension(_filepath);
        var ext = Path.GetExtension(_filepath);
        var newFilename = $"{noExt}-Removed{ext}";
        return Path.Combine(dir, newFilename);
    }
}

internal class MetaDataFlags
{
    internal static MetadataFlags KeepOldMaxStack;
    internal static MetadataFlags PreserveAll;
}