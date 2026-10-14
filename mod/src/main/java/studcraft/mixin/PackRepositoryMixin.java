package studcraft.mixin;

import java.util.ArrayList;
import java.util.List;
import net.minecraft.client.Minecraft;
import net.minecraft.server.packs.PackResources;
import net.minecraft.server.packs.repository.PackRepository;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
import studcraft.RobloxPack;

/** hooks.pack_inject: the Roblox pack goes on top of the client's resource packs. */
@Mixin(PackRepository.class)
public abstract class PackRepositoryMixin {
	@Inject(method = "openAllSelected", at = @At("RETURN"), cancellable = true)
	private void studcraft$addRobloxPack(CallbackInfoReturnable<List<PackResources>> cir) {
		Minecraft mc = Minecraft.getInstance();
		if (mc == null || mc.getResourcePackRepository() != (Object) this) return;
		List<PackResources> packs = new ArrayList<>(cir.getReturnValue());
		packs.add(new RobloxPack());
		cir.setReturnValue(packs);
	}
}
