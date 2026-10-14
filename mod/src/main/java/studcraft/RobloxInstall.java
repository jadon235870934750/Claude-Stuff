package studcraft;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.attribute.FileTime;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Optional;
import java.util.stream.Stream;
import studcraft.gen.Sheets;

/**
 * Finds the player's own Roblox install and reads files from it. Nothing is copied out of it:
 * every read goes straight to the file in the Roblox folder.
 */
public final class RobloxInstall {
	/** Set by Melty from {game:roblox}; a Roblox root, its Versions folder, or one version folder. */
	public static final String ENV = "STUDCRAFT_ROBLOX_DIR";
	public static final String PROPERTY = "studcraft.robloxDir";
	private static final String MARKER = Sheets.FILE_STUDS.path();

	private static Optional<Path> found;

	private RobloxInstall() {}

	public static synchronized Optional<Path> versionDir() {
		if (found == null) {
			found = locate();
			if (found.isPresent()) {
				Studcraft.LOG.info("Studcraft: reading Roblox content from {}", found.get());
			} else {
				Studcraft.LOG.warn("Studcraft: no Roblox install found (set {} to your Roblox folder). Minecraft stays vanilla.", ENV);
			}
		}
		return found;
	}

	public static boolean present() {
		return versionDir().isPresent();
	}

	/** Bytes of a sheet row's file, or null when this Roblox version does not have it. */
	public static byte[] read(Sheets.RobloxFile file) {
		Optional<Path> dir = versionDir();
		if (dir.isEmpty()) return null;
		Path p = dir.get().resolve(file.path());
		try {
			return Files.readAllBytes(p);
		} catch (IOException e) {
			Studcraft.LOG.warn("Studcraft: Roblox file {} missing ({})", file.path(), e.toString());
			return null;
		}
	}

	private static Optional<Path> locate() {
		List<Path> candidates = new ArrayList<>();
		addIfSet(candidates, System.getProperty(PROPERTY));
		addIfSet(candidates, System.getenv(ENV));
		String local = System.getenv("LOCALAPPDATA");
		if (local != null) {
			for (String launcher : new String[] {"Roblox", "Bloxstrap", "Fishstrap", "Voidstrap"}) {
				candidates.add(Path.of(local, launcher));
			}
		}
		for (String pf : new String[] {System.getenv("ProgramFiles(x86)"), System.getenv("ProgramFiles")}) {
			if (pf != null) candidates.add(Path.of(pf, "Roblox"));
		}
		candidates.add(Path.of("/Applications/Roblox.app/Contents/Resources"));
		for (Path c : candidates) {
			Optional<Path> v = resolve(c);
			if (v.isPresent()) return v;
		}
		return Optional.empty();
	}

	private static void addIfSet(List<Path> list, String value) {
		if (value != null && !value.isBlank()) {
			try {
				list.add(Path.of(value.trim()));
			} catch (RuntimeException ignored) {
				// not a usable path
			}
		}
	}

	/** A version folder itself, or the newest version folder under a root or Versions folder. */
	private static Optional<Path> resolve(Path dir) {
		if (!Files.isDirectory(dir)) return Optional.empty();
		if (Files.isRegularFile(dir.resolve(MARKER))) return Optional.of(dir);
		Path versions = Files.isDirectory(dir.resolve("Versions")) ? dir.resolve("Versions") : dir;
		try (Stream<Path> children = Files.list(versions)) {
			return children
				.filter(p -> Files.isRegularFile(p.resolve(MARKER)))
				.max(Comparator.comparing(RobloxInstall::modified));
		} catch (IOException e) {
			return Optional.empty();
		}
	}

	private static FileTime modified(Path p) {
		try {
			return Files.getLastModifiedTime(p.resolve(MARKER));
		} catch (IOException e) {
			return FileTime.fromMillis(0);
		}
	}
}
