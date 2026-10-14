package studcraft;

import java.awt.image.BufferedImage;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;
import javax.imageio.ImageIO;
import studcraft.gen.Sheets;

/** Turns a vanilla block texture into a Roblox part face, following the block_styles sheet. */
public final class BlockStyler {
	private static final List<Sheets.BlockStyle> STYLES = Sheets.BLOCK_STYLES;
	private static final Map<String, Pattern> PATTERNS = new HashMap<>();
	/** studs.dds tile index -> brightness multipliers (tile mean = 1.0), cached per output size. */
	private static final Map<Long, float[]> SHADES = new HashMap<>();
	private static BufferedImage studsAtlas;
	private static boolean studsTried;

	static {
		for (Sheets.BlockStyle s : STYLES) PATTERNS.put(s.id(), Pattern.compile(s.match()));
	}

	private BlockStyler() {}

	/** The sheet row that applies to this texture. */
	public static Sheets.BlockStyle styleFor(String name, BufferedImage img, boolean hasMcmeta) {
		boolean square = img.getWidth() == img.getHeight();
		boolean alpha = hasAlpha(img);
		for (Sheets.BlockStyle s : STYLES) {
			boolean req = switch (s.requires()) {
				case "has_mcmeta" -> hasMcmeta;
				case "not_square" -> !square;
				case "has_alpha" -> alpha;
				case "opaque" -> !alpha;
				default -> true;
			};
			if (req && PATTERNS.get(s.id()).matcher(name).matches()) return s;
		}
		return null;
	}

	/** PNG bytes of the restyled texture, or null to keep the vanilla one. */
	public static byte[] style(String name, byte[] vanillaPng, boolean hasMcmeta) throws IOException {
		BufferedImage src = ImageIO.read(new ByteArrayInputStream(vanillaPng));
		if (src == null) return null;
		Sheets.BlockStyle style = styleFor(name, src, hasMcmeta);
		if (style == null || style.surface().equals("keep")) return null;
		int[] avg = averageColor(src);
		boost(avg, style.saturation());
		int size = style.outputSize();
		BufferedImage out = new BufferedImage(size, size, BufferedImage.TYPE_INT_ARGB);
		float[] shade = style.studsTile() >= 0 ? shade(style.studsTile(), size) : null;
		if (style.studsTile() >= 0 && shade == null) return null; // Roblox studs unavailable
		for (int y = 0; y < size; y++) {
			for (int x = 0; x < size; x++) {
				int p = src.getRGB(x * src.getWidth() / size, y * src.getHeight() / size);
				int a = p >>> 24;
				if (a == 0) {
					out.setRGB(x, y, 0);
					continue;
				}
				float m = style.colorMix();
				float r = lerp((p >> 16) & 255, avg[0], m);
				float g = lerp((p >> 8) & 255, avg[1], m);
				float b = lerp(p & 255, avg[2], m);
				float k = shade != null ? shade[y * size + x] : 1.0F;
				out.setRGB(x, y, Dds.argb(a, clamp(r * k), clamp(g * k), clamp(b * k)));
			}
		}
		ByteArrayOutputStream bytes = new ByteArrayOutputStream();
		ImageIO.write(out, "png", bytes);
		return bytes.toByteArray();
	}

	private static synchronized float[] shade(int tile, int size) {
		long key = ((long) tile << 32) | size;
		if (SHADES.containsKey(key)) return SHADES.get(key);
		if (!studsTried) {
			studsTried = true;
			byte[] dds = RobloxInstall.read(Sheets.FILE_STUDS);
			if (dds != null) {
				try {
					studsAtlas = Dds.decode(dds);
				} catch (RuntimeException e) {
					Studcraft.LOG.warn("Studcraft: could not decode Roblox studs.dds", e);
				}
			}
		}
		if (studsAtlas == null) return null;
		int w = studsAtlas.getWidth();
		int y0 = tile * w;
		if (y0 + w > studsAtlas.getHeight()) return null;
		float[] lum = new float[size * size];
		double total = 0;
		int cell = w / size;
		for (int y = 0; y < size; y++) {
			for (int x = 0; x < size; x++) {
				double sum = 0;
				for (int dy = 0; dy < cell; dy++) {
					for (int dx = 0; dx < cell; dx++) {
						int p = studsAtlas.getRGB(x * cell + dx, y0 + y * cell + dy);
						sum += 0.299 * ((p >> 16) & 255) + 0.587 * ((p >> 8) & 255) + 0.114 * (p & 255);
					}
				}
				lum[y * size + x] = (float) (sum / (cell * cell));
				total += lum[y * size + x];
			}
		}
		float mean = (float) (total / lum.length);
		for (int i = 0; i < lum.length; i++) lum[i] = mean > 0 ? lum[i] / mean : 1.0F;
		SHADES.put(key, lum);
		return lum;
	}

	private static boolean hasAlpha(BufferedImage img) {
		for (int y = 0; y < img.getHeight(); y++)
			for (int x = 0; x < img.getWidth(); x++)
				if ((img.getRGB(x, y) >>> 24) < 255) return true;
		return false;
	}

	private static int[] averageColor(BufferedImage img) {
		long r = 0, g = 0, b = 0, n = 0;
		for (int y = 0; y < img.getHeight(); y++) {
			for (int x = 0; x < img.getWidth(); x++) {
				int p = img.getRGB(x, y);
				if ((p >>> 24) < 128) continue;
				r += (p >> 16) & 255;
				g += (p >> 8) & 255;
				b += p & 255;
				n++;
			}
		}
		if (n == 0) return new int[] {255, 255, 255};
		return new int[] {(int) (r / n), (int) (g / n), (int) (b / n)};
	}

	/** Pushes a colour away from its grey, the way Roblox's plastic reads brighter than Minecraft's. */
	private static void boost(int[] c, float saturation) {
		float grey = 0.299F * c[0] + 0.587F * c[1] + 0.114F * c[2];
		for (int i = 0; i < 3; i++) c[i] = clamp(grey + (c[i] - grey) * saturation);
	}

	private static float lerp(float a, float b, float t) {
		return a + (b - a) * t;
	}

	private static int clamp(float v) {
		return Math.max(0, Math.min(255, Math.round(v)));
	}
}
