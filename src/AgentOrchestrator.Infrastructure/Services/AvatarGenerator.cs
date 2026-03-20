using System.Text;

namespace AgentOrchestrator.Infrastructure.Services;

public static class AvatarGenerator
{
    private static readonly string[] SkinColors =
        { "#FDDCB5", "#F5C5A3", "#E8B88A", "#D4A574", "#C08B5C", "#A67B5B", "#8B6242", "#6B4226" };

    private static readonly string[] HairColors =
        { "#1A1A2E", "#2C1810", "#3D2314", "#6B3A2A", "#8B4513", "#A0522D", "#D4A843", "#E8D08A", "#C04030", "#9E9E9E", "#B8860B" };

    private static readonly string[] EyeColors =
        { "#4A90D9", "#2E6EB5", "#5B8C5A", "#3D7A3D", "#6B4226", "#8B7355", "#7A8B99", "#2F4F4F" };

    private static readonly string[] BgColors =
        { "#E8F4FD", "#FDE8E8", "#E8FDE8", "#FDF8E8", "#F0E8FD", "#E8F0FD", "#FDE8F4", "#E8FDFA", "#F5F0E8", "#EEE8FD" };

    public static string Generate(string seed, bool isDeveloper = false, bool isCeo = false)
    {
        var sb = new StringBuilder();

        // Deterministic feature selection from seed
        var faceShape = Pick(seed, 0, 3);       // 0=round, 1=oval, 2=wide
        var skinIdx = Pick(seed, 1, SkinColors.Length);
        var hairStyle = Pick(seed, 2, 5);       // 0=short, 1=medium, 2=long, 3=bald, 4=curly
        var hairIdx = Pick(seed, 3, HairColors.Length);
        var eyeIdx = Pick(seed, 4, EyeColors.Length);
        var hasGlasses = Pick(seed, 5, 3) == 0;
        var mouthStyle = Pick(seed, 6, 3);      // 0=smile, 1=grin, 2=neutral
        var noseSize = Pick(seed, 7, 3);         // 0=small, 1=medium, 2=large
        var bgIdx = Pick(seed, 8, BgColors.Length);
        var eyebrowStyle = Pick(seed, 9, 3);     // 0=thin, 1=thick, 2=arched
        var hasFreckles = Pick(seed, 10, 4) == 0;
        var earSize = Pick(seed, 11, 2);          // 0=small, 1=large
        var cheekColor = Pick(seed, 12, 2) == 0;

        var skin = SkinColors[skinIdx];
        var skinShadow = DarkenColor(skin, 0.15);
        var hair = HairColors[hairIdx];
        var eyes = EyeColors[eyeIdx];
        var bg = BgColors[bgIdx];

        // Face dimensions based on shape
        int faceRx, faceRy;
        switch (faceShape)
        {
            case 0: faceRx = 58; faceRy = 58; break;   // round
            case 1: faceRx = 50; faceRy = 64; break;   // oval
            default: faceRx = 62; faceRy = 56; break;   // wide
        }

        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200">""");

        // Background
        sb.AppendLine($"""  <rect width="200" height="200" rx="20" fill="{bg}"/>""");

        // Ears (behind face)
        var earR = earSize == 0 ? 10 : 14;
        sb.AppendLine($"""  <ellipse cx="{100 - faceRx - 2}" cy="115" rx="{earR}" ry="{earR + 2}" fill="{skin}"/>""");
        sb.AppendLine($"""  <ellipse cx="{100 + faceRx + 2}" cy="115" rx="{earR}" ry="{earR + 2}" fill="{skin}"/>""");
        sb.AppendLine($"""  <ellipse cx="{100 - faceRx - 2}" cy="115" rx="{earR - 3}" ry="{earR - 1}" fill="{skinShadow}"/>""");
        sb.AppendLine($"""  <ellipse cx="{100 + faceRx + 2}" cy="115" rx="{earR - 3}" ry="{earR - 1}" fill="{skinShadow}"/>""");

        // Hair behind head (for long/medium styles)
        if (hairStyle == 2) // long
        {
            sb.AppendLine($"""  <ellipse cx="100" cy="115" rx="{faceRx + 8}" ry="{faceRy + 18}" fill="{hair}"/>""");
            sb.AppendLine($"""  <rect x="{100 - faceRx - 6}" y="90" width="{(faceRx + 6) * 2}" height="80" rx="8" fill="{hair}"/>""");
        }

        // Face
        sb.AppendLine($"""  <ellipse cx="100" cy="115" rx="{faceRx}" ry="{faceRy}" fill="{skin}"/>""");

        // Cheeks
        if (cheekColor)
        {
            sb.AppendLine("""  <ellipse cx="72" cy="125" rx="12" ry="8" fill="#FFB6B6" opacity="0.35"/>""");
            sb.AppendLine("""  <ellipse cx="128" cy="125" rx="12" ry="8" fill="#FFB6B6" opacity="0.35"/>""");
        }

        // Freckles
        if (hasFreckles)
        {
            sb.AppendLine($"""  <circle cx="78" cy="118" r="1.5" fill="{skinShadow}"/>""");
            sb.AppendLine($"""  <circle cx="85" cy="121" r="1.2" fill="{skinShadow}"/>""");
            sb.AppendLine($"""  <circle cx="82" cy="115" r="1.3" fill="{skinShadow}"/>""");
            sb.AppendLine($"""  <circle cx="118" cy="117" r="1.5" fill="{skinShadow}"/>""");
            sb.AppendLine($"""  <circle cx="122" cy="121" r="1.2" fill="{skinShadow}"/>""");
            sb.AppendLine($"""  <circle cx="115" cy="114" r="1.3" fill="{skinShadow}"/>""");
        }

        // Eyes - whites
        sb.AppendLine("""  <ellipse cx="78" cy="108" rx="10" ry="11" fill="white"/>""");
        sb.AppendLine("""  <ellipse cx="122" cy="108" rx="10" ry="11" fill="white"/>""");

        // Eyes - iris
        sb.AppendLine($"""  <circle cx="78" cy="108" r="6" fill="{eyes}"/>""");
        sb.AppendLine($"""  <circle cx="122" cy="108" r="6" fill="{eyes}"/>""");

        // Eyes - pupil
        sb.AppendLine("""  <circle cx="78" cy="108" r="3" fill="#1A1A2E"/>""");
        sb.AppendLine("""  <circle cx="122" cy="108" r="3" fill="#1A1A2E"/>""");

        // Eyes - highlight
        sb.AppendLine("""  <circle cx="81" cy="106" r="2" fill="white" opacity="0.7"/>""");
        sb.AppendLine("""  <circle cx="125" cy="106" r="2" fill="white" opacity="0.7"/>""");

        // Eyebrows
        var browWidth = eyebrowStyle switch { 0 => "1.5", 1 => "3", _ => "2" };
        var browD = eyebrowStyle == 2
            ? "M66 96 Q78 88 90 96"   // arched
            : "M66 95 Q78 90 90 95";  // flatter
        var browD2 = eyebrowStyle == 2
            ? "M110 96 Q122 88 134 96"
            : "M110 95 Q122 90 134 95";
        sb.AppendLine($"""  <path d="{browD}" fill="none" stroke="{hair}" stroke-width="{browWidth}" stroke-linecap="round"/>""");
        sb.AppendLine($"""  <path d="{browD2}" fill="none" stroke="{hair}" stroke-width="{browWidth}" stroke-linecap="round"/>""");

        // Nose
        var noseD = noseSize switch
        {
            0 => "M97 115 Q100 121 103 115",
            1 => "M95 113 Q100 124 105 113",
            _ => "M93 112 Q100 127 107 112"
        };
        sb.AppendLine($"""  <path d="{noseD}" fill="none" stroke="{skinShadow}" stroke-width="2" stroke-linecap="round"/>""");

        // Mouth
        var mouthD = mouthStyle switch
        {
            0 => "M83 132 Q100 145 117 132",  // smile
            1 => "M80 130 Q100 148 120 130",   // grin
            _ => "M85 134 Q100 139 115 134"    // neutral
        };
        sb.AppendLine($"""  <path d="{mouthD}" fill="none" stroke="{skinShadow}" stroke-width="2.5" stroke-linecap="round"/>""");
        if (mouthStyle == 1) // grin shows teeth
        {
            sb.AppendLine("""  <path d="M86 132 Q100 142 114 132" fill="white" stroke="none"/>""");
        }

        // Glasses
        if (hasGlasses)
        {
            sb.AppendLine("""  <circle cx="78" cy="108" r="15" fill="none" stroke="#333" stroke-width="2.5"/>""");
            sb.AppendLine("""  <circle cx="122" cy="108" r="15" fill="none" stroke="#333" stroke-width="2.5"/>""");
            sb.AppendLine("""  <path d="M93 108 L107 108" fill="none" stroke="#333" stroke-width="2.5"/>""");
            sb.AppendLine($"""  <path d="M63 108 L{100 - faceRx} 106" fill="none" stroke="#333" stroke-width="2.5"/>""");
            sb.AppendLine($"""  <path d="M137 108 L{100 + faceRx} 106" fill="none" stroke="#333" stroke-width="2.5"/>""");
        }

        // Hair on top (drawn last to overlay face top)
        switch (hairStyle)
        {
            case 0: // short
                sb.AppendLine($"""  <ellipse cx="100" cy="72" rx="{faceRx + 4}" ry="28" fill="{hair}"/>""");
                sb.AppendLine($"""  <rect x="{100 - faceRx - 2}" y="72" width="{(faceRx + 2) * 2}" height="22" fill="{hair}"/>""");
                break;
            case 1: // medium
                sb.AppendLine($"""  <ellipse cx="100" cy="72" rx="{faceRx + 6}" ry="30" fill="{hair}"/>""");
                sb.AppendLine($"""  <rect x="{100 - faceRx - 4}" y="72" width="{(faceRx + 4) * 2}" height="28" fill="{hair}"/>""");
                // side hair
                sb.AppendLine($"""  <rect x="{100 - faceRx - 4}" y="86" width="16" height="40" rx="6" fill="{hair}"/>""");
                sb.AppendLine($"""  <rect x="{100 + faceRx - 12}" y="86" width="16" height="40" rx="6" fill="{hair}"/>""");
                break;
            case 2: // long (back already drawn, just the top)
                sb.AppendLine($"""  <ellipse cx="100" cy="72" rx="{faceRx + 8}" ry="30" fill="{hair}"/>""");
                sb.AppendLine($"""  <rect x="{100 - faceRx - 6}" y="72" width="{(faceRx + 6) * 2}" height="26" fill="{hair}"/>""");
                // side hair
                sb.AppendLine($"""  <rect x="{100 - faceRx - 6}" y="84" width="18" height="60" rx="8" fill="{hair}"/>""");
                sb.AppendLine($"""  <rect x="{100 + faceRx - 12}" y="84" width="18" height="60" rx="8" fill="{hair}"/>""");
                break;
            case 3: // bald - just a subtle hairline
                sb.AppendLine($"""  <ellipse cx="100" cy="80" rx="{faceRx - 5}" ry="8" fill="{skinShadow}" opacity="0.3"/>""");
                break;
            case 4: // curly
                sb.AppendLine($"""  <ellipse cx="100" cy="75" rx="{faceRx + 6}" ry="30" fill="{hair}"/>""");
                for (int i = 0; i < 8; i++)
                {
                    var cx = 60 + i * 11;
                    var cy = 58 + (i % 2 == 0 ? 0 : 5);
                    sb.AppendLine($"""  <circle cx="{cx}" cy="{cy}" r="10" fill="{hair}"/>""");
                }
                for (int i = 0; i < 6; i++)
                {
                    var cx = 66 + i * 13;
                    sb.AppendLine($"""  <circle cx="{cx}" cy="48" r="8" fill="{hair}"/>""");
                }
                break;
        }

        // --- Role badges ---
        if (isDeveloper)
        {
            // </> code badge in top-right corner
            sb.AppendLine("""  <rect x="155" y="8" width="38" height="24" rx="6" fill="#1a1a2e" opacity="0.85"/>""");
            sb.AppendLine("""  <text x="174" y="25" text-anchor="middle" fill="#4ADE80" font-family="monospace" font-size="13" font-weight="bold">&lt;/&gt;</text>""");
        }

        if (isCeo)
        {
            // Gold star badge in top-right corner
            sb.AppendLine("""  <polygon points="174,8 178,18 189,19 181,26 183,37 174,32 165,37 167,26 159,19 170,18" fill="#FFD700" stroke="#B8860B" stroke-width="1"/>""");
        }

        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private static int Pick(string seed, int featureIndex, int optionCount)
    {
        int hash = featureIndex * 7919;
        for (int i = 0; i < seed.Length; i++)
        {
            hash = hash * 31 + seed[i] + featureIndex * 17;
        }
        return Math.Abs(hash) % optionCount;
    }

    private static string DarkenColor(string hex, double amount)
    {
        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);

        r = (int)(r * (1 - amount));
        g = (int)(g * (1 - amount));
        b = (int)(b * (1 - amount));

        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
