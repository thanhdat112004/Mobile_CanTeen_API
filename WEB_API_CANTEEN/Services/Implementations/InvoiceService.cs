using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using WEB_API_CANTEEN.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace WEB_API_CANTEEN.Services
{
    internal class InvoiceVm
    {
        public long OrderId { get; set; }
        public string? Username { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public decimal Total { get; set; }
        public List<InvoiceItemVm> Items { get; set; } = new();
    }

    internal class InvoiceItemVm
    {
        public long ItemId { get; set; }
        public string Name { get; set; } = "";
        public int Qty { get; set; }
        public decimal Price { get; set; }
        public decimal Subtotal => Price * Qty;
        public string? Note { get; set; }
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IConfiguration _cfg;

        public InvoiceService(SmartCanteenDbContext ctx, IConfiguration cfg)
        {
            _ctx = ctx;
            _cfg = cfg;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateInvoice(long orderId)
        {
            var order = _ctx.Orders.FirstOrDefault(o => o.Id == orderId)
                        ?? throw new InvalidOperationException("Order not found");

            var user = _ctx.Users.FirstOrDefault(u => u.Id == order.UserId);

            var items = _ctx.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .Select(oi => new InvoiceItemVm
                {
                    ItemId = oi.ItemId,
                    Name = oi.Item.Name,
                    Qty = oi.Qty,
                    Price = oi.Item.Price,
                    Note = oi.Note
                })
                .ToList();

            var createdAt = order.CreatedAt;
            if (createdAt == default) createdAt = DateTime.UtcNow;

            var vm = new InvoiceVm
            {
                OrderId = order.Id,
                Username = user?.Username,
                CreatedAt = createdAt,
                Status = order.Status ?? "",
                PaymentStatus = order.PaymentStatus ?? "",
                PaymentMethod = order.PaymentMethod ?? "",
                Total = order.Total,
                Items = items
            };

            var qrBytes = GenerateOrderQr(vm.OrderId, vm.Total);

            using var ms = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(36);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // Header
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("SMART CANTEEN").FontSize(18).SemiBold();
                            col.Item().Text("Hóa đơn thanh toán").FontSize(14);
                            col.Item().Text(t =>
                            {
                                t.Span("Mã đơn: ").SemiBold();
                                t.Span($"#{vm.OrderId}");
                            });
                        });

                        if (qrBytes != null)
                            row.ConstantItem(110).AlignRight().Image(qrBytes);
                    });

                    // Nội dung
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Text(t => { t.Span("Khách hàng: ").SemiBold(); t.Span(vm.Username ?? "N/A"); });
                        col.Item().Text(t => { t.Span("Thời gian: ").SemiBold(); t.Span(vm.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")); });
                        col.Item().Text(t => { t.Span("Thanh toán: ").SemiBold(); t.Span($"{vm.PaymentMethod} — {vm.PaymentStatus}"); });
                        col.Item().Text(t => { t.Span("Trạng thái đơn: ").SemiBold(); t.Span(vm.Status); });

                        col.Item().LineHorizontal(0.5f);
                        col.Spacing(5);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(5);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(3);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Món").SemiBold();
                                header.Cell().AlignRight().Text("SL").SemiBold();
                                header.Cell().AlignRight().Text("Đơn giá").SemiBold();
                                header.Cell().AlignRight().Text("Thành tiền").SemiBold();
                                header.Cell().Text("Ghi chú").SemiBold();

                                header.Cell().ColumnSpan(5).PaddingTop(2).LineHorizontal(0.5f);
                            });

                            foreach (var it in vm.Items)
                            {
                                table.Cell().Text(it.Name);
                                table.Cell().AlignRight().Text(it.Qty.ToString());
                                table.Cell().AlignRight().Text(string.Format("{0:N0} đ", it.Price));
                                table.Cell().AlignRight().Text(string.Format("{0:N0} đ", it.Subtotal));
                                table.Cell().Text(it.Note ?? "");
                            }

                            table.Cell().ColumnSpan(5).PaddingTop(2).LineHorizontal(0.5f);
                            table.Cell().ColumnSpan(3).Text("");
                            table.Cell().AlignRight().Text("Tổng cộng:").SemiBold();
                            table.Cell().AlignRight().Text(string.Format("{0:N0} đ", vm.Total)).SemiBold();
                            table.Cell().Text("");
                        });

                        col.Spacing(10);
                        col.Item().Text("Cảm ơn bạn đã ủng hộ căng-tin!");
                    });

                    // Footer: KHÔNG chain .FontSize(...) sau Text()
                    page.Footer().AlignCenter().Column(footerCol =>
                    {
                        footerCol.Item().Text(t =>
                        {
                            t.Span("© ");
                            t.Span(DateTime.UtcNow.Year.ToString());
                            t.Span(" Smart Canteen — ");
                            t.Span("Hóa đơn được tạo tự động.");
                        });
                    });
                });
            }).GeneratePdf(ms);

            return ms.ToArray();
        }

        private byte[]? GenerateOrderQr(long orderId, decimal total)
        {
            try
            {
                var payload = $"ORDER:{orderId}|AMT:{total}";
                using var gen = new QRCodeGenerator();
                var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
                var png = new PngByteQRCode(data);
                return png.GetGraphic(5);
            }
            catch { return null; }
        }
    }
}
