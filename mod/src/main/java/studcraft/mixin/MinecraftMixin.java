package studcraft.mixin;

import net.minecraft.client.Minecraft;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
import studcraft.TestMode;

/** hooks.test_driver: drives Melty's test mode; does nothing outside a Melty test run. */
@Mixin(Minecraft.class)
public abstract class MinecraftMixin {
	@Inject(method = "tick", at = @At("HEAD"))
	private void studcraft$testMode(CallbackInfo ci) {
		TestMode.tick((Minecraft) (Object) this);
	}
}
