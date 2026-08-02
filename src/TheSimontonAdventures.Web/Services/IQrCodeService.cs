namespace TheSimontonAdventures.Web.Services;

public interface IQrCodeService
{
    string BuildPublicUrl(string qrSlug);

    string GenerateSvg(string qrSlug);

    byte[] GeneratePng(string qrSlug);
}