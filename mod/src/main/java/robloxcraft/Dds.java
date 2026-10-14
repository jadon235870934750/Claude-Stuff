package robloxcraft;

import java.awt.image.BufferedImage;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

/** Decodes the top mip level of the DXT1 DDS files Roblox ships (studs.dds, sky512_*.tex). */
public final class Dds {
	private Dds() {}

	public static BufferedImage decode(byte[] data) {
		ByteBuffer b = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN);
		if (data.length < 128 || b.getInt(0) != 0x20534444) throw new IllegalArgumentException("not a DDS file");
		int height = b.getInt(12);
		int width = b.getInt(16);
		int fourCC = b.getInt(84);
		if (fourCC != 0x31545844) throw new IllegalArgumentException("DDS is not DXT1");
		BufferedImage img = new BufferedImage(width, height, BufferedImage.TYPE_INT_ARGB);
		int off = 128;
		int[] cols = new int[4];
		for (int by = 0; by < height; by += 4) {
			for (int bx = 0; bx < width; bx += 4) {
				int c0 = b.getShort(off) & 0xFFFF;
				int c1 = b.getShort(off + 2) & 0xFFFF;
				int bits = b.getInt(off + 4);
				off += 8;
				int r0 = ((c0 >> 11) & 31) * 255 / 31, g0 = ((c0 >> 5) & 63) * 255 / 63, b0 = (c0 & 31) * 255 / 31;
				int r1 = ((c1 >> 11) & 31) * 255 / 31, g1 = ((c1 >> 5) & 63) * 255 / 63, b1 = (c1 & 31) * 255 / 31;
				cols[0] = argb(255, r0, g0, b0);
				cols[1] = argb(255, r1, g1, b1);
				if (c0 > c1) {
					cols[2] = argb(255, (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3);
					cols[3] = argb(255, (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3);
				} else {
					cols[2] = argb(255, (r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2);
					cols[3] = 0;
				}
				for (int i = 0; i < 16; i++) {
					int x = bx + (i & 3), y = by + (i >> 2);
					if (x < width && y < height) img.setRGB(x, y, cols[(bits >>> (2 * i)) & 3]);
				}
			}
		}
		return img;
	}

	static int argb(int a, int r, int g, int b) {
		return (a << 24) | (r << 16) | (g << 8) | b;
	}
}
