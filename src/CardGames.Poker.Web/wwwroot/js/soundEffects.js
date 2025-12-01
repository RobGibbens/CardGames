// Sound effects module for poker game
window.SoundEffectsModule = {
    audioContext: null,

    getContext: function() {
        if (!this.audioContext) {
            try {
                this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
            } catch (e) {
                console.warn('Audio context not available:', e);
                return null;
            }
        }
        return this.audioContext;
    },

    playTone: function(frequency, durationMs, volume) {
        const ctx = this.getContext();
        if (!ctx) return;

        try {
            const oscillator = ctx.createOscillator();
            const gainNode = ctx.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(ctx.destination);

            oscillator.frequency.value = frequency;
            oscillator.type = 'sine';

            const safeVolume = Math.min(1, Math.max(0, volume));
            const durationSec = durationMs / 1000.0;

            gainNode.gain.setValueAtTime(safeVolume * 0.3, ctx.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + durationSec);

            oscillator.start(ctx.currentTime);
            oscillator.stop(ctx.currentTime + durationSec);
        } catch (e) {
            // Silently fail if audio playback not available
        }
    },

    resumeContext: function() {
        const ctx = this.getContext();
        if (ctx && ctx.state === 'suspended') {
            ctx.resume();
        }
    }
};
