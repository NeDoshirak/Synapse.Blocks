using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;

namespace Synapse.Blocks.Api.MarkdownPages;

public sealed class MarkdownDemoSeeder(AppDbContext db, IHostEnvironment environment)
{
    private const string BuiltInKey = "superadmin-rich-markdown-demo-v1";
    private static readonly Guid DemoPageId = Guid.Parse("c41f3074-b158-4f71-a8dc-ccfbc08a01d1");
    private static readonly Guid DemoVersionId = Guid.Parse("2f8031bc-4e88-4c1f-9f2b-ec01bc135f7c");
    private static readonly Guid SignalAssetId = Guid.Parse("dd892851-fd7d-461b-8c6c-6e314f0ad845");
    private static readonly Guid GridAssetId = Guid.Parse("fdf8cf83-76a6-45ae-a799-577bfdb2f361");

    public async Task EnsureSeededAsync(string platformAdminId, CancellationToken cancellationToken = default)
    {
        if (await db.MarkdownPages.AnyAsync(page => page.BuiltInKey == BuiltInKey, cancellationToken)) return;

        var assetsPath = Path.Combine(environment.ContentRootPath, "MarkdownPages", "DemoAssets");
        var signalBytes = await File.ReadAllBytesAsync(Path.Combine(assetsPath, "signal-path.png"), cancellationToken);
        var gridBytes = await File.ReadAllBytesAsync(Path.Combine(assetsPath, "grid-pattern.png"), cancellationToken);
        if (await db.MarkdownAssets.AnyAsync(asset => asset.Id == SignalAssetId || asset.Id == GridAssetId, cancellationToken))
            throw new InvalidOperationException("Markdown demo asset IDs are already in use.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        db.MarkdownAssets.AddRange(
            new MarkdownAsset { Id = SignalAssetId, OwnerId = platformAdminId, ContentType = "image/png", Length = signalBytes.LongLength, Content = signalBytes, CreatedAt = now },
            new MarkdownAsset { Id = GridAssetId, OwnerId = platformAdminId, ContentType = "image/png", Length = gridBytes.LongLength, Content = gridBytes, CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken);

        var page = new MarkdownPage
        {
            Id = DemoPageId,
            OwnerId = platformAdminId,
            Title = "Демо: устройство сигнальной сети",
            IsBuiltIn = true,
            BuiltInKey = BuiltInKey,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.MarkdownPages.Add(page);
        await db.SaveChangesAsync(cancellationToken);

        var version = new MarkdownPageVersion
        {
            Id = DemoVersionId,
            PageId = page.Id,
            Version = 1,
            Title = page.Title,
            Markdown = $$"""
                # Как устроена передача сигнала

                Эта страница показывает, как **Markdown** может объяснять тему, объединяя текст, таблицы, код и изображения.

                ![Схема передачи сигнала](asset:{{SignalAssetId:D}})

                ## Путь сообщения

                1. Устройство отправляет короткий пакет.
                2. Узел усиливает и передаёт его дальше.
                3. Получатель подтверждает доставку.

                | Узел | Задача | Состояние |
                |:--|:--|:--:|
                | Датчик | Создаёт сообщение | Готов |
                | Ретранслятор | Усиливает сигнал | Готов |
                | Приёмник | Получает сообщение | Ожидает |

                - [x] Источник подключён
                - [ ] Приёмник ответил

                ```csharp
                var packet = new SignalPacket("hello", sequence: 3);
                relay.Forward(packet);
                ```

                ## Формула затухания

                Для расстояния $d$ и коэффициента среды $k$ уровень сигнала можно оценить так:

                $$P(d) = P_0 e^{-kd}$$

                ```mermaid
                flowchart LR
                    A[Датчик] --> B[Ретранслятор]
                    B --> C[Приёмник]
                ```

                ![Сетка ячеек и узлов](asset:{{GridAssetId:D}})

                > Наблюдение: если пакет потерялся, повторная передача должна сохранить его номер последовательности.
                """,
            CreatedAt = now
        };
        db.MarkdownPageVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        page.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
