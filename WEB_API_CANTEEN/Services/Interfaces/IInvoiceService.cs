// IInvoiceService: định nghĩa dịch vụ xuất hóa đơn PDF
namespace WEB_API_CANTEEN.Services
{
    public interface IInvoiceService
    {
        byte[] GenerateInvoice(long orderId);
    }
}
