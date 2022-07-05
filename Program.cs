using CommandLine;
using CommandLine.Text;
using FlutterAppSettings.Command;

var parser = new Parser(settings =>
{
    settings.CaseInsensitiveEnumValues = true;
    settings.AutoHelp = true;
});

var parserResult = parser.ParseArguments<BootstrapCommand, ReflectCommand>(args);
await parserResult
    .MapResult(
        command =>
        {
            try
            {
                return command switch
                {
                    BootstrapCommand bootstrapCommand =>
                        bootstrapCommand.ExecuteAsync(),
                    ReflectCommand reflectCommand =>
                        reflectCommand.ExecuteAsync(),
                    _ => throw new NotImplementedException($"{command} is not implemented.")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        },
        errors =>
        {
            foreach (var err in errors)
            {
                Console.WriteLine(err?.ToString());
            }

            DisplayHelp(parserResult);

            return Task.FromResult(1);
        });

void DisplayHelp(ParserResult<object> result)
{
    var helpText = HelpText.AutoBuild(result, h =>
    {
        h.AddEnumValuesToHelpText = true;
        return HelpText.DefaultParsingErrorsHandler(result, h);
    }, e => e);
    Console.WriteLine(helpText);
}