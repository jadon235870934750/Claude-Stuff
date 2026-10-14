package robloxcraft;

import java.awt.image.BufferedImage;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import javax.imageio.ImageIO;
import net.minecraft.client.Minecraft;
import net.minecraft.network.chat.Component;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.packs.PackLocationInfo;
import net.minecraft.server.packs.PackResources;
import net.minecraft.server.packs.PackType;
import net.minecraft.server.packs.metadata.MetadataSectionSerializer;
import net.minecraft.server.packs.repository.PackSource;
import net.minecraft.server.packs.resources.IoSupplier;
import org.jetbrains.annotations.Nullable;
import robloxcraft.gen.Sheets;

/**
 * An in-memory resource pack that sits on top of Minecraft's own. Every answer is made on the fly from
 * the player's Roblox install (and, for block faces, from Minecraft's own texture underneath).
 */
public final class RobloxPack implements PackResources {
	public static final String ID = "robloxcraft";
	private static final String BLOCK_DIR = "textures/block";
	private static final PackLocationInfo INFO = new PackLocationInfo(
		ID, Component.literal("Robloxcraft (your Roblox install)"), PackSource.BUILT_IN, Optional.empty());
	/** texture name -> restyled PNG, or empty when the vanilla texture stays. */
	private static final Map<String, Optional<byte[]>> BLOCK_CACHE = new ConcurrentHashMap<>();
	private static final Map<ResourceLocation, Sheets.AssetOverride> OVERRIDES = new HashMap<>();

	static {
		for (Sheets.AssetOverride o : Sheets.ASSET_OVERRIDES) OVERRIDES.put(ResourceLocation.parse(o.target()), o);
	}

	@Nullable
	@Override
	public IoSupplier<InputStream> getRootResource(String... path) {
		return null;
	}

	@Nullable
	@Override
	public IoSupplier<InputStream> getResource(PackType type, ResourceLocation loc) {
		if (type != PackType.CLIENT_RESOURCES || !RobloxInstall.present()) return null;
		Sheets.AssetOverride o = OVERRIDES.get(loc);
		if (o != null) {
			byte[] bytes = override(o);
			return bytes == null ? null : () -> new ByteArrayInputStream(bytes);
		}
		if (loc.getNamespace().equals("minecraft") && loc.getPath().startsWith(BLOCK_DIR + "/") && loc.getPath().endsWith(".png")) {
			byte[] bytes = block(loc);
			return bytes == null ? null : () -> new ByteArrayInputStream(bytes);
		}
		return null;
	}

	@Override
	public void listResources(PackType type, String namespace, String path, ResourceOutput output) {
		if (type != PackType.CLIENT_RESOURCES || !RobloxInstall.present()) return;
		for (Map.Entry<ResourceLocation, Sheets.AssetOverride> e : OVERRIDES.entrySet()) {
			ResourceLocation loc = e.getKey();
			if (e.getValue().listed() && loc.getNamespace().equals(namespace) && loc.getPath().startsWith(path + "/")) {
				byte[] bytes = override(e.getValue());
				if (bytes != null) output.accept(loc, () -> new ByteArrayInputStream(bytes));
			}
		}
		if (!namespace.equals("minecraft")) return;
		if (!(path.equals(BLOCK_DIR) || path.startsWith(BLOCK_DIR + "/") || BLOCK_DIR.startsWith(path + "/"))) return;
		vanilla().listResources(type, namespace, path, (loc, supplier) -> {
			if (!loc.getPath().startsWith(BLOCK_DIR + "/") || !loc.getPath().endsWith(".png")) return;
			byte[] bytes = block(loc);
			if (bytes != null) output.accept(loc, () -> new ByteArrayInputStream(bytes));
		});
	}

	@Override
	public Set<String> getNamespaces(PackType type) {
		return type == PackType.CLIENT_RESOURCES ? Set.of("minecraft", ID) : Set.of();
	}

	@Nullable
	@Override
	public <T> T getMetadataSection(MetadataSectionSerializer<T> serializer) {
		return null;
	}

	@Override
	public PackLocationInfo location() {
		return INFO;
	}

	@Override
	public void close() {}

	private static PackResources vanilla() {
		return Minecraft.getInstance().getVanillaPackResources();
	}

	@Nullable
	private static byte[] block(ResourceLocation loc) {
		String file = loc.getPath().substring(BLOCK_DIR.length() + 1);
		String name = file.substring(0, file.length() - ".png".length());
		return BLOCK_CACHE.computeIfAbsent(file, f -> {
			try {
				IoSupplier<InputStream> src = vanilla().getResource(PackType.CLIENT_RESOURCES, loc);
				if (src == null) return Optional.empty();
				byte[] png;
				try (InputStream in = src.get()) {
					png = in.readAllBytes();
				}
				boolean mcmeta = vanilla().getResource(PackType.CLIENT_RESOURCES, loc.withSuffix(".mcmeta")) != null;
				return Optional.ofNullable(BlockStyler.style(name, png, mcmeta));
			} catch (IOException | RuntimeException e) {
				Robloxcraft.LOG.warn("Robloxcraft: kept vanilla {} ({})", loc, e.toString());
				return Optional.empty();
			}
		}).orElse(null);
	}

	@Nullable
	private static byte[] override(Sheets.AssetOverride o) {
		byte[] src = RobloxInstall.read(Sheets.file(o.file()));
		if (src == null) return null;
		try {
			return switch (o.transform()) {
				case "copy_bytes" -> src;
				case "jpg_to_png" -> png(ImageIO.read(new ByteArrayInputStream(src)));
				case "moon_phase_grid" -> moonGrid(ImageIO.read(new ByteArrayInputStream(src)));
				case "font_definition" -> fontDefinition();
				default -> throw new IllegalStateException("unknown transform " + o.transform());
			};
		} catch (IOException | RuntimeException e) {
			Robloxcraft.LOG.warn("Robloxcraft: could not build {} ({})", o.target(), e.toString());
			return null;
		}
	}

	private static byte[] moonGrid(BufferedImage moon) throws IOException {
		int s = moon.getWidth();
		BufferedImage grid = new BufferedImage(s * 4, s * 2, BufferedImage.TYPE_INT_ARGB);
		for (int cy = 0; cy < 2; cy++)
			for (int cx = 0; cx < 4; cx++)
				for (int y = 0; y < s; y++)
					for (int x = 0; x < s; x++)
						grid.setRGB(cx * s + x, cy * s + y, moon.getRGB(x, y) | 0xFF000000);
		return png(grid);
	}

	/** Stacks on top of vanilla's default.json: FontManager puts the top pack's providers first. */
	private static byte[] fontDefinition() {
		String json = """
			{"providers": [
			  {"type": "reference", "id": "minecraft:include/space"},
			  {"type": "ttf", "file": "robloxcraft:roblox_ui.ttf", "size": 9.5, "oversample": 4.0, "shift": [0.0, 1.0], "skip": ""}
			]}
			""";
		return json.getBytes(StandardCharsets.UTF_8);
	}

	private static byte[] png(BufferedImage img) throws IOException {
		if (img == null) throw new IOException("unreadable image");
		ByteArrayOutputStream out = new ByteArrayOutputStream();
		ImageIO.write(img, "png", out);
		return out.toByteArray();
	}
}
