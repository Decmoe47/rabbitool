using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Rabbitool.Repository.Subscribe;

/// <summary>
///     many to many的约定命名方式改成了：A_B
/// </summary>
/// <param name="dependencies"></param>
public class MyManyToManyJoinEntityTypeConvention(ProviderConventionSetBuilderDependencies dependencies)
    : ManyToManyJoinEntityTypeConvention(dependencies)
{
    protected override string GenerateJoinTypeName(IConventionSkipNavigation skipNavigation)
    {
        IConventionSkipNavigation inverse = skipNavigation.Inverse!;
        IConventionEntityType declaringEntityType1 = skipNavigation.DeclaringEntityType;
        IConventionEntityType declaringEntityType2 = inverse!.DeclaringEntityType;
        IConventionModel model = declaringEntityType1.Model;
        string x = !declaringEntityType1.HasSharedClrType
            ? declaringEntityType1.ClrType.ShortDisplayName()
            : declaringEntityType1.ShortName();
        x = x.Replace("Entity", "");
        string y = !declaringEntityType2.HasSharedClrType
            ? declaringEntityType2.ClrType.ShortDisplayName()
            : declaringEntityType2.ShortName();
        y = y.Replace("Entity", "");
        string joinTypeName = StringComparer.Ordinal.Compare(x, y) < 0 ? x + "_" + y : y + "_" + x;
        if (model.FindEntityType(joinTypeName) != null)
        {
            Dictionary<string, int> dictionary = model.GetEntityTypes()
                .ToDictionary((Func<IConventionEntityType, string>)(et => et.Name), _ => 0);
            joinTypeName = Uniquifier.Uniquify(joinTypeName, dictionary, int.MaxValue);
        }

        return joinTypeName;
    }
}