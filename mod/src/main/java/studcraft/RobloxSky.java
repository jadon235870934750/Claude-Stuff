package studcraft;

import com.mojang.blaze3d.platform.NativeImage;
import com.mojang.blaze3d.systems.RenderSystem;
import com.mojang.blaze3d.vertex.BufferBuilder;
import com.mojang.blaze3d.vertex.BufferUploader;
import com.mojang.blaze3d.vertex.DefaultVertexFormat;
import com.mojang.blaze3d.vertex.Tesselator;
import com.mojang.blaze3d.vertex.VertexFormat;
import java.awt.image.BufferedImage;
import java.util.HashMap;
import java.util.Map;
import net.minecraft.client.Minecraft;
import net.minecraft.client.multiplayer.ClientLevel;
import net.minecraft.client.renderer.GameRenderer;
import net.minecraft.client.renderer.texture.DynamicTexture;
import net.minecraft.resources.ResourceLocation;
import org.joml.Matrix4f;
import studcraft.gen.Sheets;

/** Draws Roblox's default skybox (read from the player's Roblox install) as the Overworld sky. */
public final class RobloxSky {
	private static final float RADIUS = 100.0F;
	private static final int GL_TEXTURE_2D = 0x0DE1;
	private static final int GL_TEXTURE_WRAP_S = 0x2802;
	private static final int GL_TEXTURE_WRAP_T = 0x2803;
	private static final int GL_CLAMP_TO_EDGE = 0x812F;
	private static final Map<String, ResourceLocation> TEXTURES = new HashMap<>();
	private static boolean loaded;
	private static boolean available;
	private static long framesDrawn;

	private RobloxSky() {}

	private static void load() {
		loaded = true;
		if (!RobloxInstall.present()) return;
		for (Sheets.SkyFace face : Sheets.SKY_FACES) {
			byte[] dds = RobloxInstall.read(Sheets.file(face.file()));
			if (dds == null) return;
			BufferedImage img;
			try {
				img = Dds.decode(dds);
			} catch (RuntimeException e) {
				Studcraft.LOG.warn("Studcraft: could not decode Roblox sky {}", face.file(), e);
				return;
			}
			NativeImage n = new NativeImage(img.getWidth(), img.getHeight(), false);
			for (int y = 0; y < img.getHeight(); y++) {
				for (int x = 0; x < img.getWidth(); x++) {
					int p = img.getRGB(x, y);
					// NativeImage stores ABGR
					n.setPixelRGBA(x, y, 0xFF000000 | ((p & 0xFF) << 16) | (p & 0xFF00) | ((p >> 16) & 0xFF));
				}
			}
			ResourceLocation id = ResourceLocation.fromNamespaceAndPath(RobloxPack.ID, "sky/" + face.id());
			DynamicTexture tex = new DynamicTexture(n);
			tex.setFilter(true, false);
			Minecraft.getInstance().getTextureManager().register(id, tex);
			// clamp so bilinear filtering does not bleed the opposite edge into the seams
			tex.bind();
			RenderSystem.texParameter(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
			RenderSystem.texParameter(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
			TEXTURES.put(face.id(), id);
		}
		available = true;
		Studcraft.LOG.info("Studcraft: Roblox skybox loaded");
	}

	/** True once the Roblox skybox is on screen (loads it on first call). */
	public static boolean active() {
		if (!loaded) load();
		return available;
	}

	/** Called by LevelRendererMixin right after vanilla draws its flat sky dome. */
	public static void render(Matrix4f view, float partialTick) {
		if (!active()) return;
		ClientLevel level = Minecraft.getInstance().level;
		if (level == null) return;
		float light = level.getSkyDarken(partialTick);
		int c = Math.round(Math.max(0.0F, Math.min(1.0F, light)) * 255.0F);
		RenderSystem.disableBlend();
		RenderSystem.setShaderColor(1.0F, 1.0F, 1.0F, 1.0F);
		RenderSystem.setShader(GameRenderer::getPositionTexColorShader);
		Tesselator tesselator = Tesselator.getInstance();
		for (Sheets.SkyFace face : Sheets.SKY_FACES) {
			RenderSystem.setShaderTexture(0, TEXTURES.get(face.id()));
			float[][] k = face.corners();
			BufferBuilder b = tesselator.begin(VertexFormat.Mode.QUADS, DefaultVertexFormat.POSITION_TEX_COLOR);
			b.addVertex(view, k[0][0] * RADIUS, k[0][1] * RADIUS, k[0][2] * RADIUS).setUv(0.0F, 0.0F).setColor(c, c, c, 255);
			b.addVertex(view, k[1][0] * RADIUS, k[1][1] * RADIUS, k[1][2] * RADIUS).setUv(0.0F, 1.0F).setColor(c, c, c, 255);
			b.addVertex(view, k[2][0] * RADIUS, k[2][1] * RADIUS, k[2][2] * RADIUS).setUv(1.0F, 1.0F).setColor(c, c, c, 255);
			b.addVertex(view, k[3][0] * RADIUS, k[3][1] * RADIUS, k[3][2] * RADIUS).setUv(1.0F, 0.0F).setColor(c, c, c, 255);
			BufferUploader.drawWithShader(b.buildOrThrow());
		}
		framesDrawn++;
	}

	public static long framesDrawn() {
		return framesDrawn;
	}
}
