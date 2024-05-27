﻿using ECommons.DalamudServices;
using RotationSolver.Basic.Configuration;
using RotationSolver.Data;
using RotationSolver.Localization;
using RotationSolver.Updaters;

namespace RotationSolver.Commands;

public static partial class RSCommands
{
    private static void DoOtherCommand(OtherCommandType otherType, string str)
    {
        switch (otherType)
        {
            case OtherCommandType.Rotations:
                var customCombo = DataCenter.RightNowRotation;
                if (customCombo == null) return;

                DoRotationCommand(customCombo, str);
                break;

            case OtherCommandType.DoActions:
                DoActionCommand(str);
                break;

            case OtherCommandType.ToggleActions:
                ToggleActionCommand(str);
                break;

            case OtherCommandType.Settings:
                DoSettingCommand(str);
                break;

            case OtherCommandType.NextAction:
                DoAction();
                break;
        }
    }

    private static void DoSettingCommand(string str)
    {
        var strs = str.Split(' ');
        var settingName = strs[0];
        var value = strs.LastOrDefault();
        foreach (var property in typeof(Configs).GetRuntimeProperties().Where(p => p.GetMethod?.IsPublic ?? false))
        {
            if (!settingName.Equals(property.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            var type = property.PropertyType;
            if (type == typeof(ConditionBoolean))
                type = typeof(bool);

            object convertedValue = null;
            bool valueParsedSuccessfully = true;

            if (type.IsEnum)
            {
                valueParsedSuccessfully = Enum.TryParse(type, value, ignoreCase: true, out var parsedEnum);
                if (valueParsedSuccessfully)
                {
                    convertedValue = parsedEnum;
                }
            }
            else
            {
                try
                {
                    convertedValue = Convert.ChangeType(value, type);
                }
                catch
                {
                    valueParsedSuccessfully = false;
                }
            }

            if (!valueParsedSuccessfully)
            {
                if (type == typeof(bool))
                {
                    var config = property.GetValue(Service.Config) as ConditionBoolean;
                    if (config != null)
                    {
                        config.Value = !config.Value;
                        convertedValue = config.Value;
                    }
                }
                else if (type.IsEnum)
                {
                    // If invalid enum value provided - increment to the next enum value
                    var currentEnumValue = (Enum)property.GetValue(Service.Config);
                    convertedValue = GetNextEnumValue(currentEnumValue);
                }
            }

            if (convertedValue == null)
            {
#if DEBUG
                Svc.Chat.Print("Failed to parse the value.");
#endif
                continue;
            }

            if (property.PropertyType == typeof(ConditionBoolean))
            {
                var relay = (ConditionBoolean)property.GetValue(Service.Config)!;
                relay.Value = (bool)convertedValue;
                convertedValue = relay;
            }

            property.SetValue(Service.Config, convertedValue);
            value = convertedValue.ToString();

            Svc.Chat.Print(string.Format(UiString.CommandsChangeSettingsValue.Local(), property.Name, value));

            return;
        }

        Svc.Chat.PrintError(UiString.CommandsCannotFindConfig.Local());
    }

    private static Enum GetNextEnumValue(Enum currentEnumValue)
    {
        var enumValues = Enum.GetValues(currentEnumValue.GetType()).Cast<Enum>().ToArray();
        var nextIndex = Array.IndexOf(enumValues, currentEnumValue) + 1;

        return enumValues.Length == nextIndex ? enumValues[0] : enumValues[nextIndex];
    }

    private static void ToggleActionCommand(string str)
    {
        foreach (var act in RotationUpdater.RightRotationActions)
        {
            if (str.StartsWith(act.Name))
            {
                var flag = str[act.Name.Length..].Trim();

                act.IsEnabled = bool.TryParse(flag, out var parse) ? parse : !act.IsEnabled;

                if (Service.Config.ShowToggledActionInChat)
                {
                    Svc.Chat.Print($"Toggled {act.Name} : {act.IsEnabled}");
                }

                return;
            }
        }
    }

    private static void DoActionCommand(string str)
    {
        var strs = str.Split('-');

        if (strs != null && strs.Length == 2 && double.TryParse(strs[1], out var time))
        {
            var actName = strs[0];
            foreach (var iAct in RotationUpdater.RightRotationActions)
            {
                if (actName == iAct.Name)
                {
                    DataCenter.AddCommandAction(iAct, time);

                    if (Service.Config.ShowToastsAboutDoAction)
                    {
                        Svc.Toasts.ShowQuest(string.Format(UiString.CommandsInsertAction.Local(), time),
                            new Dalamud.Game.Gui.Toast.QuestToastOptions()
                            {
                                IconId = iAct.IconID,
                            });
                    }

                    return;
                }
            }
        }

        Svc.Chat.PrintError(UiString.CommandsInsertActionFailure.Local());
    }


    private static void DoRotationCommand(ICustomRotation customCombo, string str)
    {
        var configs = customCombo.Configs;
        foreach (var config in configs)
        {
            if (config.DoCommand(configs, str))
            {
                Svc.Chat.Print(string.Format(UiString.CommandsChangeSettingsValue.Local(),
                    config.DisplayName, config.Value));

                return;
            }
        }

        Svc.Chat.PrintError(UiString.CommandsInsertActionFailure.Local());
    }
}