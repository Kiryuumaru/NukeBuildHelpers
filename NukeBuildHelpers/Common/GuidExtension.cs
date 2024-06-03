namespace NukeBuildHelpers.Common;

public static class GuidEncoder
{
    public static string Encode(string guidText)
    {
        Guid guid = new(guidText);
        return Encode(guid);
    }

    public static string Encode(this Guid guid)
    {
        string enc = Convert.ToBase64String(guid.ToByteArray());
        enc = enc.Replace("/", "_");
        enc = enc.Replace("+", "-");
        return enc[..22];
    }

    public static Guid Decode(string encoded)
    {
        encoded = encoded.Replace("_", "/");
        encoded = encoded.Replace("-", "+");
        byte[] buffer = Convert.FromBase64String(encoded + "==");
        return new Guid(buffer);
    }
}
