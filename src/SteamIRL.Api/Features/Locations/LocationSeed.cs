using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SteamIRL.Api.Features.Locations;

// Provenance and review limits: docs/location-sources.md. No photos or copied descriptions.
public static class LocationSeed
{
    public static readonly Guid WhiteBridgeId = Guid.Parse("aaaa1111-1111-4111-8111-111111111111");
    public static readonly Guid KaukysosId = Guid.Parse("aaaa2222-2222-4222-8222-222222222222");
    private static readonly Guid Volleyball = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Basketball = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Football = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid TableTennis = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset CheckedAt = new(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

    public static void Configure(EntityTypeBuilder<Location> builder) => builder.HasData(
        new Location
        {
            Id = WhiteBridgeId, Name = "Площадки у Белого моста", Address = "Upės g. 6, Vilnius",
            Description = "Открытый спортивный комплекс у реки Нерис с площадками для пляжного волейбола и баскетбола.",
            Latitude = 54.6948m, Longitude = 25.2764m, ApprovalStatus = ApprovalStatus.Approved, UpdatedAt = CheckedAt,
            SourceUrl = "https://baltasistiltas.lt/bendra-informacija/", SourceCheckedAt = CheckedAt,
            CoordinateNote = "Ориентировочная точка комплекса, не отдельной площадки или входа. Уточни место встречи по адресу.",
            AccessNotes = "Оператор публикует правила очереди и ограничения для тренировок. Перед поездкой проверь действующие условия по ссылке на источник."
        },
        new Location
        {
            Id = KaukysosId, Name = "Спортивный комплекс Kaukysos", Address = "Kaukysos g., Vilnius",
            Description = "Общественный спортивный комплекс в районе Паплауя. В материалах Active Vilnius указаны футбол, баскетбол, пляжный волейбол и настольный теннис.",
            Latitude = 54.6764m, Longitude = 25.3161m, ApprovalStatus = ApprovalStatus.Approved, UpdatedAt = CheckedAt,
            SourceUrl = "https://activevilnius.lt/AV-pristatymas.pdf", SourceCheckedAt = CheckedAt,
            CoordinateNote = "Ориентировочная точка района комплекса. Точный вход и отдельные площадки требуют проверки на месте.",
            AccessNotes = "Сведения об активностях взяты из презентации оператора. Текущий доступ, состояние оборудования и часы работы требуют уточнения."
        });

    public static readonly LocationActivity[] Links = [
        new() { LocationId = WhiteBridgeId, ActivityId = Volleyball },
        new() { LocationId = WhiteBridgeId, ActivityId = Basketball },
        new() { LocationId = KaukysosId, ActivityId = Volleyball },
        new() { LocationId = KaukysosId, ActivityId = Basketball },
        new() { LocationId = KaukysosId, ActivityId = Football },
        new() { LocationId = KaukysosId, ActivityId = TableTennis }
    ];
}
