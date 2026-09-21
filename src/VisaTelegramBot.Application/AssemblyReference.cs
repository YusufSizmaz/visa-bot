using System.Reflection;

namespace VisaTelegramBot.Application;

/// <summary>Assembly taramasi (MediatR, FluentValidation, mimari testleri) icin sabit referans noktasi.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
