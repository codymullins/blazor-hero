// Audio Engine - Web Audio API wrapper for precise audio timing

class AudioEngine {
    constructor() {
        this.audioContext = null;
        this.songBuffer = null;
        this.songSource = null;
        this.gainNode = null;
        this.startTime = 0;
        this.pauseTime = 0;
        this.isPlaying = false;
        this.songOffset = 0;

        // Sound effect buffers
        this.sfxBuffers = new Map();
        this.sfxGain = null;

        // Song-specific sound buffers (takes priority over default sfxBuffers)
        this.songSfxBuffers = new Map();
        this.currentSongSounds = null;  // Current noteSounds config

        // Hold note sustain sounds (one per lane)
        this.holdSustains = new Map();  // lane -> { oscillators, gainNode }
        this.songHoldFreqs = null;  // Song-specific frequencies for holds
    }

    async initialize() {
        this.audioContext = new (window.AudioContext || window.webkitAudioContext)();

        // Main gain node for song
        this.gainNode = this.audioContext.createGain();
        this.gainNode.connect(this.audioContext.destination);

        // SFX gain node
        this.sfxGain = this.audioContext.createGain();
        this.sfxGain.connect(this.audioContext.destination);

        // Generate synthesized guitar sounds
        this._generateGuitarSounds();

        // NOTE: Don't await resume here - it will block until user gesture
        // The context will be resumed when playSong() is called after user interaction
        console.log('[AudioEngine] Initialized, context state:', this.audioContext.state);
    }

    // Generate guitar-like sounds using Web Audio API synthesis
    _generateGuitarSounds() {
        // Guitar string frequencies (like open strings, low to high)
        // Lane 0 (Green) = Low E string feel
        // Lane 1 (Red) = A string feel  
        // Lane 2 (Yellow) = D string feel
        // Lane 3 (Blue) = G string feel
        // Lane 4 (Orange) = B string feel (highest, Expert only)
        const laneNotes = [
            { baseFreq: 164.81, name: 'E3' },  // Lane 0 - Green (low)
            { baseFreq: 220.00, name: 'A3' },  // Lane 1 - Red
            { baseFreq: 293.66, name: 'D4' },  // Lane 2 - Yellow
            { baseFreq: 392.00, name: 'G4' },  // Lane 3 - Blue
            { baseFreq: 493.88, name: 'B4' },  // Lane 4 - Orange (high)
        ];

        // Generate hit sounds for each lane
        for (let lane = 0; lane < 5; lane++) {
            const note = laneNotes[lane];
            
            // Perfect hit - bright, crisp with more harmonics
            this.sfxBuffers.set(`hit_${lane}_perfect`, this._createGuitarStrum({
                baseFreq: note.baseFreq,
                harmonics: [1, 0.5, 0.35, 0.25, 0.15, 0.1],
                duration: 0.3,
                attack: 0.003,
                decay: 0.06,
                brightness: 1.3
            }));

            // Good hit - slightly muted, fewer harmonics
            this.sfxBuffers.set(`hit_${lane}_good`, this._createGuitarStrum({
                baseFreq: note.baseFreq,
                harmonics: [1, 0.35, 0.2, 0.1],
                duration: 0.25,
                attack: 0.005,
                decay: 0.08,
                brightness: 1.0
            }));
        }

        // Miss - dissonant buzz/muted string sound
        this.sfxBuffers.set('miss', this._createMissSound());

        // Thump - muted string hit when pressing with no note
        this.sfxBuffers.set('thump', this._createThumpSound());

        // Star power activation - rising power chord
        this.sfxBuffers.set('starpower', this._createStarPowerSound());

        console.log('[AudioEngine] Generated synthesized guitar sounds for 4 lanes (default fallback)');
    }

    // Load song-specific sounds based on chart noteSounds config
    loadSongSounds(noteSoundsConfig) {
        // Clear any previous song-specific sounds
        this.clearSongSounds();

        if (!noteSoundsConfig || !noteSoundsConfig.lanes) {
            console.log('[AudioEngine] No song-specific sounds config, using defaults');
            return;
        }

        this.currentSongSounds = noteSoundsConfig;
        const style = noteSoundsConfig.style || 'guitar';
        const brightness = noteSoundsConfig.brightness || 1.0;
        const attack = noteSoundsConfig.attack || 0.01;
        const sustain = noteSoundsConfig.sustain || 0.6;

        // Store frequencies and style for hold notes
        this.songHoldFreqs = noteSoundsConfig.lanes.map(l => l.freq);
        this.songHoldStyle = style;

        console.log(`[AudioEngine] Generating ${style} sounds for song`);

        // Generate sounds for each lane based on style
        for (let lane = 0; lane < noteSoundsConfig.lanes.length; lane++) {
            const laneConfig = noteSoundsConfig.lanes[lane];
            const freq = laneConfig.freq;

            let perfectBuffer, goodBuffer;

            switch (style) {
                case 'soft_synth':
                    perfectBuffer = this._createSoftSynthSound(freq, { brightness: brightness * 1.2, attack, sustain, duration: 0.35 });
                    goodBuffer = this._createSoftSynthSound(freq, { brightness: brightness * 0.9, attack: attack * 1.5, sustain: sustain * 0.8, duration: 0.3 });
                    break;

                case 'synthwave':
                    perfectBuffer = this._createSynthwaveSound(freq, { brightness: brightness * 1.2, attack, sustain, duration: 0.3 });
                    goodBuffer = this._createSynthwaveSound(freq, { brightness: brightness * 0.9, attack: attack * 1.5, sustain: sustain * 0.8, duration: 0.25 });
                    break;

                case 'power_chord':
                    perfectBuffer = this._createPowerChordSound(freq, { brightness: brightness * 1.2, attack, sustain, duration: 0.35 });
                    goodBuffer = this._createPowerChordSound(freq, { brightness, attack: attack * 1.5, sustain: sustain * 0.8, duration: 0.3 });
                    break;

                default:
                    // Use default guitar style
                    perfectBuffer = this._createGuitarStrum({
                        baseFreq: freq,
                        harmonics: [1, 0.5, 0.35, 0.25, 0.15, 0.1],
                        duration: 0.3,
                        attack: attack,
                        decay: 0.06,
                        brightness: brightness * 1.3
                    });
                    goodBuffer = this._createGuitarStrum({
                        baseFreq: freq,
                        harmonics: [1, 0.35, 0.2, 0.1],
                        duration: 0.25,
                        attack: attack * 1.5,
                        decay: 0.08,
                        brightness: brightness
                    });
            }

            this.songSfxBuffers.set(`hit_${lane}_perfect`, perfectBuffer);
            this.songSfxBuffers.set(`hit_${lane}_good`, goodBuffer);
        }

        console.log(`[AudioEngine] Generated song-specific ${style} sounds for ${noteSoundsConfig.lanes.length} lanes`);
    }

    // Clear song-specific sounds
    clearSongSounds() {
        this.songSfxBuffers.clear();
        this.currentSongSounds = null;
        this.songHoldFreqs = null;
        this.songHoldStyle = null;
    }

    // Soft synth sound - warm, gentle tones for tutorial
    _createSoftSynthSound(freq, params) {
        const { brightness, attack, sustain, duration } = params;
        const sampleRate = this.audioContext.sampleRate;
        const numSamples = Math.floor(sampleRate * duration);
        const buffer = this.audioContext.createBuffer(1, numSamples, sampleRate);
        const data = buffer.getChannelData(0);

        for (let i = 0; i < numSamples; i++) {
            const t = i / sampleRate;
            let sample = 0;

            // Pure sine with gentle harmonics
            sample += Math.sin(2 * Math.PI * freq * t) * 1.0;
            sample += Math.sin(2 * Math.PI * freq * 2 * t) * 0.3 * brightness;
            sample += Math.sin(2 * Math.PI * freq * 3 * t) * 0.1 * brightness;

            // Soft triangle wave blend for warmth
            const tri = (2 * Math.abs(2 * ((freq * t) % 1) - 1) - 1);
            sample += tri * 0.2;

            // Smooth ADSR envelope
            let envelope;
            if (t < attack) {
                envelope = t / attack;
            } else if (t < attack + 0.1) {
                envelope = 1 - ((t - attack) / 0.1) * (1 - sustain);
            } else if (t < duration - 0.15) {
                envelope = sustain;
            } else {
                envelope = sustain * (1 - (t - (duration - 0.15)) / 0.15);
            }

            data[i] = sample * envelope * 0.35;
        }

        return buffer;
    }

    // Synthwave sound - bright, punchy with detune
    _createSynthwaveSound(freq, params) {
        const { brightness, attack, sustain, duration } = params;
        const sampleRate = this.audioContext.sampleRate;
        const numSamples = Math.floor(sampleRate * duration);
        const buffer = this.audioContext.createBuffer(1, numSamples, sampleRate);
        const data = buffer.getChannelData(0);

        for (let i = 0; i < numSamples; i++) {
            const t = i / sampleRate;
            let sample = 0;

            // Square-ish wave with detune for that synthwave character
            const sq1 = Math.sign(Math.sin(2 * Math.PI * freq * t));
            const sq2 = Math.sign(Math.sin(2 * Math.PI * freq * 1.005 * t));
            sample += sq1 * 0.4;
            sample += sq2 * 0.25;

            // Add bright harmonics
            sample += Math.sin(2 * Math.PI * freq * 2 * t) * 0.2 * brightness;
            sample += Math.sin(2 * Math.PI * freq * 4 * t) * 0.1 * brightness;

            // Punchy envelope
            let envelope;
            if (t < attack) {
                envelope = t / attack;
            } else if (t < attack + 0.05) {
                envelope = 1 - ((t - attack) / 0.05) * 0.3;
            } else {
                const releaseTime = t - attack - 0.05;
                envelope = 0.7 * Math.exp(-releaseTime * 6);
            }

            data[i] = sample * envelope * 0.3;
        }

        return buffer;
    }

    // Power chord sound - aggressive with fifth and distortion
    _createPowerChordSound(freq, params) {
        const { brightness, attack, sustain, duration } = params;
        const sampleRate = this.audioContext.sampleRate;
        const numSamples = Math.floor(sampleRate * duration);
        const buffer = this.audioContext.createBuffer(1, numSamples, sampleRate);
        const data = buffer.getChannelData(0);

        const fifth = freq * 1.5;  // Perfect fifth
        const octave = freq * 2;

        for (let i = 0; i < numSamples; i++) {
            const t = i / sampleRate;
            let sample = 0;

            // Root, fifth, and octave
            sample += Math.sin(2 * Math.PI * freq * t) * 0.5;
            sample += Math.sin(2 * Math.PI * fifth * t) * 0.4;
            sample += Math.sin(2 * Math.PI * octave * t) * 0.25;

            // Add harmonics for grit
            for (let h = 2; h <= 5; h++) {
                sample += Math.sin(2 * Math.PI * freq * h * t) * (0.15 / h) * brightness;
            }

            // Soft clipping for mild distortion
            sample = Math.tanh(sample * 1.5);

            // Aggressive envelope
            let envelope;
            if (t < attack) {
                envelope = t / attack;
            } else if (t < attack + 0.03) {
                envelope = 1;
            } else {
                const releaseTime = t - attack - 0.03;
                envelope = Math.exp(-releaseTime * 5) * sustain + (1 - sustain) * Math.exp(-releaseTime * 15);
            }

            // Pick attack noise
            if (t < 0.008) {
                sample += (Math.random() - 0.5) * 0.4 * (1 - t / 0.008);
            }

            data[i] = sample * envelope * 0.4;
        }

        return buffer;
    }

    // Create a guitar strum sound with Karplus-Strong-like synthesis
    _createGuitarStrum(params) {
        const { baseFreq, harmonics, duration, attack, decay, brightness } = params;
        const sampleRate = this.audioContext.sampleRate;
        const numSamples = Math.floor(sampleRate * duration);
        const buffer = this.audioContext.createBuffer(1, numSamples, sampleRate);
        const data = buffer.getChannelData(0);

        // Generate the sound with harmonics for a richer guitar tone
        for (let i = 0; i < numSamples; i++) {
            const t = i / sampleRate;
            let sample = 0;

            // Add harmonics to create guitar-like timbre
            for (let h = 0; h < harmonics.length; h++) {
                const freq = baseFreq * (h + 1);
                const amplitude = harmonics[h] * brightness;
                // Add slight detuning for more realistic sound
                const detune = 1 + (Math.random() - 0.5) * 0.002;
                sample += amplitude * Math.sin(2 * Math.PI * freq * detune * t);
            }

            // Apply ADSR envelope (fast attack, quick decay, sustain, release)
            let envelope;
            if (t < attack) {
                // Attack phase
                envelope = t / attack;
            } else if (t < attack + decay) {
                // Decay phase
                const decayProgress = (t - attack) / decay;
                envelope = 1 - decayProgress * 0.4; // Decay to 60%
            } else {
                // Release phase - exponential decay like a plucked string
                const releaseTime = t - attack - decay;
                envelope = 0.6 * Math.exp(-releaseTime * 8);
            }

            // Add slight noise burst at attack for pick sound
            if (t < 0.01) {
                sample += (Math.random() - 0.5) * 0.3 * (1 - t / 0.01);
            }

            data[i] = sample * envelope * 0.4; // Scale down to prevent clipping
        }

        return buffer;
    }

    // Create a miss/buzz sound (muted string or fret buzz)
    _createMissSound() {
        const sampleRate = this.audioContext.sampleRate;
        const duration = 0.25;
        const numSamples = Math.floor(sampleRate * duration);
        const buffer = this.audioContext.createBuffer(1, numSamples, sampleRate);
        const data = buffer.getChannelData(0);

        // Use mid-range frequencies for better audibility on all speakers
        const buzzFreq = 150;
        const buzzFreq2 = 165; // Slightly detuned for dissonance
        const buzzFreq3 = 220; // Higher harmonic for presence

        for (let i = 0; i < numSamples; i++) {
            const t = i / sampleRate;

            // Dissonant frequencies with higher harmonics for audibility
            let sample = Math.sin(2 * Math.PI * buzzFreq * t) * 0.35;
            sample += Math.sin(2 * Math.PI * buzzFreq2 * t) * 0.3;
            sample += Math.sin(2 * Math.PI * buzzFreq3 * t) * 0.2;

            // Add filtered noise for buzz/scratch character
            const noise = (Math.random() - 0.5) * 0.5;
            sample += noise;

            // Quick decay envelope with initial punch
            let envelope;
            if (t < 0.01) {
                envelope = t / 0.01; // Quick attack
            } else {
                envelope = Math.exp(-(t - 0.01) * 12);
            }

            data[i] = sample * envelope * 0.6;
        }

        return buffer;
    }

    // Create a muted thump sound for pressing keys with no note
    _createThumpSound() {
        const sampleRate = this.audioContext.sampleRate;
        const duration = 0.08;
        const numSamples = Math.floor(sampleRate * duration);
        const buffer = this.audioContext.createBuffer(1, numSamples, sampleRate);
        const data = buffer.getChannelData(0);

        // Low thud frequency
        const thumpFreq = 60;

        for (let i = 0; i < numSamples; i++) {
            const t = i / sampleRate;

            // Low frequency thump
            let sample = Math.sin(2 * Math.PI * thumpFreq * t) * 0.5;
            
            // Add a bit of click at the start
            if (t < 0.005) {
                sample += (Math.random() - 0.5) * 0.3;
            }

            // Very fast decay - just a quick thud
            const envelope = Math.exp(-t * 50);

            data[i] = sample * envelope * 0.7;
        }

        return buffer;
    }

    // Create star power activation sound - epic rising power chord
    _createStarPowerSound() {
        const sampleRate = this.audioContext.sampleRate;
        const duration = 0.6;
        const numSamples = Math.floor(sampleRate * duration);
        const buffer = this.audioContext.createBuffer(1, numSamples, sampleRate);
        const data = buffer.getChannelData(0);

        // Power chord frequencies (E5 power chord with octave)
        const baseFreq = 329.63;  // E4
        const fifth = 493.88;     // B4
        const octave = 659.25;    // E5

        for (let i = 0; i < numSamples; i++) {
            const t = i / sampleRate;

            // Rising pitch effect
            const pitchRise = 1 + t * 0.15;

            // Power chord with harmonics
            let sample = Math.sin(2 * Math.PI * baseFreq * pitchRise * t) * 0.4;
            sample += Math.sin(2 * Math.PI * fifth * pitchRise * t) * 0.3;
            sample += Math.sin(2 * Math.PI * octave * pitchRise * t) * 0.25;
            
            // Add some shimmer/brightness
            sample += Math.sin(2 * Math.PI * baseFreq * 2 * pitchRise * t) * 0.15;
            sample += Math.sin(2 * Math.PI * octave * 2 * pitchRise * t) * 0.1;

            // Envelope: quick attack, sustain, then fade
            let envelope;
            if (t < 0.02) {
                envelope = t / 0.02;  // Quick attack
            } else if (t < 0.3) {
                envelope = 1.0;  // Sustain
            } else {
                envelope = 1.0 - ((t - 0.3) / 0.3);  // Fade out
            }

            data[i] = sample * envelope * 0.5;
        }

        return buffer;
    }

    async ensureResumed() {
        if (this.audioContext && this.audioContext.state === 'suspended') {
            await this.audioContext.resume();
        }
    }

    async loadSong(url) {
        if (!this.audioContext) {
            console.error('[AudioEngine] loadSong called but audioContext is null');
            return 60000;
        }

        try {
            const response = await fetch(url);
            if (!response.ok) {
                console.warn(`Audio file not found: ${url}`);
                this.songBuffer = null;
                return 60000; // Return 60 seconds as default duration
            }
            const arrayBuffer = await response.arrayBuffer();
            this.songBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
            return this.songBuffer.duration * 1000; // Return duration in ms
        } catch (e) {
            console.warn(`Failed to load audio: ${url}`, e);
            this.songBuffer = null;
            return 60000; // Return 60 seconds as default duration
        }
    }

    async loadSfx(name, url) {
        try {
            const response = await fetch(url);
            const arrayBuffer = await response.arrayBuffer();
            const buffer = await this.audioContext.decodeAudioData(arrayBuffer);
            this.sfxBuffers.set(name, buffer);
            return true;
        } catch (e) {
            console.warn(`Failed to load SFX: ${name}`, e);
            return false;
        }
    }

    async playSong(offsetMs = 0) {
        if (!this.audioContext) {
            console.error('[AudioEngine] playSong called but audioContext is null');
            return;
        }

        // Resume audio context (requires user gesture - which we have since user clicked play)
        if (this.audioContext.state === 'suspended') {
            console.log('[AudioEngine] Resuming suspended context');
            await this.audioContext.resume();
        }

        this.stopSong();

        // Convert offset from ms to seconds
        const offsetSec = offsetMs / 1000;
        this.songOffset = offsetMs;

        // Record start time for precise position tracking (works even without audio)
        this.startTime = this.audioContext.currentTime - offsetSec;
        this.isPlaying = true;

        if (!this.songBuffer) {
            // No audio loaded, but we still track time
            console.log('Playing without audio (no audio file loaded)');
            return;
        }

        this.songSource = this.audioContext.createBufferSource();
        this.songSource.buffer = this.songBuffer;
        this.songSource.connect(this.gainNode);

        this.songSource.start(0, offsetSec);

        this.songSource.onended = () => {
            this.isPlaying = false;
        };
    }

    pauseSong() {
        if (this.isPlaying) {
            this.pauseTime = this.getCurrentTime();
            if (this.songSource) {
                this.songSource.stop();
                this.songSource = null;
            }
            this.isPlaying = false;
        }
    }

    async resumeSong() {
        if (!this.isPlaying && this.pauseTime > 0) {
            await this.playSong(this.pauseTime);
        }
    }

    stopSong() {
        if (this.songSource) {
            try {
                this.songSource.stop();
            } catch (e) {
                // Ignore if already stopped
            }
            this.songSource = null;
        }
        this.isPlaying = false;
        this.startTime = 0;
        this.pauseTime = 0;
    }

    // Returns current position in milliseconds - THIS IS THE AUTHORITATIVE TIME SOURCE
    getCurrentTime() {
        if (!this.isPlaying) return this.pauseTime;
        return (this.audioContext.currentTime - this.startTime) * 1000;
    }

    // Get song duration in milliseconds
    getSongDuration() {
        return this.songBuffer ? this.songBuffer.duration * 1000 : 60000;
    }

    playSfx(name, volume = 1.0) {
        // Check song-specific sounds first, then fall back to defaults
        let buffer = this.songSfxBuffers.get(name);
        if (!buffer) {
            buffer = this.sfxBuffers.get(name);
        }
        if (!buffer) return;

        const source = this.audioContext.createBufferSource();
        const gain = this.audioContext.createGain();

        source.buffer = buffer;
        gain.gain.value = volume;

        source.connect(gain);
        gain.connect(this.sfxGain);

        source.start(0);
    }

    setVolume(value) {
        if (this.gainNode) {
            this.gainNode.gain.value = Math.max(0, Math.min(1, value));
        }
    }

    setSfxVolume(value) {
        if (this.sfxGain) {
            this.sfxGain.gain.value = Math.max(0, Math.min(1, value));
        }
    }

    isContextRunning() {
        return this.audioContext && this.audioContext.state === 'running';
    }

    // Start a sustained tone for hold notes - style matches the song
    startHoldSustain(lane, volume = 0.4) {
        // Stop any existing sustain on this lane
        this.stopHoldSustain(lane);

        // Use song-specific frequencies if available, otherwise default
        const defaultFreqs = [
            164.81,  // Lane 0 - E3
            220.00,  // Lane 1 - A3
            293.66,  // Lane 2 - D4
            392.00   // Lane 3 - G4
        ];
        const baseFreq = (this.songHoldFreqs && this.songHoldFreqs[lane])
            ? this.songHoldFreqs[lane]
            : (defaultFreqs[lane] || 220);

        const style = this.songHoldStyle || 'guitar';
        const oscillators = [];
        const gainNode = this.audioContext.createGain();
        gainNode.connect(this.sfxGain);

        switch (style) {
            case 'soft_synth':
                this._createSoftSynthHold(baseFreq, oscillators, gainNode);
                break;
            case 'synthwave':
                this._createSynthwaveHold(baseFreq, oscillators, gainNode);
                break;
            case 'power_chord':
                this._createPowerChordHold(baseFreq, oscillators, gainNode);
                break;
            default:
                this._createGuitarHold(baseFreq, oscillators, gainNode);
        }

        // Fade in
        gainNode.gain.setValueAtTime(0, this.audioContext.currentTime);
        gainNode.gain.linearRampToValueAtTime(volume, this.audioContext.currentTime + 0.05);

        // Start all oscillators
        oscillators.forEach(osc => osc.start());

        // Store reference for stopping later
        this.holdSustains.set(lane, { oscillators, gainNode });
    }

    // Soft synth hold - warm, gentle pad-like sustain
    _createSoftSynthHold(baseFreq, oscillators, gainNode) {
        // Pure sine waves for warmth
        const osc1 = this.audioContext.createOscillator();
        osc1.type = 'sine';
        osc1.frequency.value = baseFreq;

        const osc2 = this.audioContext.createOscillator();
        osc2.type = 'sine';
        osc2.frequency.value = baseFreq * 2;  // Octave

        const osc3 = this.audioContext.createOscillator();
        osc3.type = 'triangle';
        osc3.frequency.value = baseFreq * 1.001;  // Slight detune for warmth

        const gain1 = this.audioContext.createGain();
        const gain2 = this.audioContext.createGain();
        const gain3 = this.audioContext.createGain();
        gain1.gain.value = 0.5;
        gain2.gain.value = 0.15;
        gain3.gain.value = 0.25;

        osc1.connect(gain1);
        osc2.connect(gain2);
        osc3.connect(gain3);
        gain1.connect(gainNode);
        gain2.connect(gainNode);
        gain3.connect(gainNode);

        // Gentle vibrato
        const lfo = this.audioContext.createOscillator();
        const lfoGain = this.audioContext.createGain();
        lfo.type = 'sine';
        lfo.frequency.value = 4;  // Slow vibrato
        lfoGain.gain.value = 2;   // Very subtle
        lfo.connect(lfoGain);
        lfoGain.connect(osc1.frequency);

        oscillators.push(osc1, osc2, osc3, lfo);
    }

    // Synthwave hold - bright, pulsing synth sustain
    _createSynthwaveHold(baseFreq, oscillators, gainNode) {
        // Square waves for that classic synth sound
        const osc1 = this.audioContext.createOscillator();
        osc1.type = 'square';
        osc1.frequency.value = baseFreq;

        const osc2 = this.audioContext.createOscillator();
        osc2.type = 'square';
        osc2.frequency.value = baseFreq * 1.005;  // Detune for thickness

        const osc3 = this.audioContext.createOscillator();
        osc3.type = 'sawtooth';
        osc3.frequency.value = baseFreq * 2;  // Bright octave

        const gain1 = this.audioContext.createGain();
        const gain2 = this.audioContext.createGain();
        const gain3 = this.audioContext.createGain();
        gain1.gain.value = 0.25;
        gain2.gain.value = 0.2;
        gain3.gain.value = 0.1;

        osc1.connect(gain1);
        osc2.connect(gain2);
        osc3.connect(gain3);
        gain1.connect(gainNode);
        gain2.connect(gainNode);
        gain3.connect(gainNode);

        // Pulsing LFO on volume for that synthwave throb
        const lfo = this.audioContext.createOscillator();
        const lfoGain = this.audioContext.createGain();
        lfo.type = 'sine';
        lfo.frequency.value = 6;  // Faster pulse
        lfoGain.gain.value = 0.15;  // Subtle volume modulation
        lfo.connect(lfoGain);
        lfoGain.connect(gainNode.gain);

        // Also add pitch vibrato
        const lfo2 = this.audioContext.createOscillator();
        const lfoGain2 = this.audioContext.createGain();
        lfo2.type = 'sine';
        lfo2.frequency.value = 5;
        lfoGain2.gain.value = 4;
        lfo2.connect(lfoGain2);
        lfoGain2.connect(osc1.frequency);
        lfoGain2.connect(osc2.frequency);

        oscillators.push(osc1, osc2, osc3, lfo, lfo2);
    }

    // Power chord hold - aggressive, distorted sustain
    _createPowerChordHold(baseFreq, oscillators, gainNode) {
        const fifth = baseFreq * 1.5;
        const octave = baseFreq * 2;

        // Root
        const osc1 = this.audioContext.createOscillator();
        osc1.type = 'sawtooth';
        osc1.frequency.value = baseFreq;

        // Fifth
        const osc2 = this.audioContext.createOscillator();
        osc2.type = 'sawtooth';
        osc2.frequency.value = fifth;

        // Octave
        const osc3 = this.audioContext.createOscillator();
        osc3.type = 'sawtooth';
        osc3.frequency.value = octave;

        // Detuned root for thickness
        const osc4 = this.audioContext.createOscillator();
        osc4.type = 'sawtooth';
        osc4.frequency.value = baseFreq * 1.003;

        const gain1 = this.audioContext.createGain();
        const gain2 = this.audioContext.createGain();
        const gain3 = this.audioContext.createGain();
        const gain4 = this.audioContext.createGain();
        gain1.gain.value = 0.3;
        gain2.gain.value = 0.25;
        gain3.gain.value = 0.15;
        gain4.gain.value = 0.2;

        // Create a waveshaper for distortion
        const distortion = this.audioContext.createWaveShaper();
        distortion.curve = this._makeDistortionCurve(20);
        distortion.oversample = '2x';

        osc1.connect(gain1);
        osc2.connect(gain2);
        osc3.connect(gain3);
        osc4.connect(gain4);
        gain1.connect(distortion);
        gain2.connect(distortion);
        gain3.connect(distortion);
        gain4.connect(distortion);
        distortion.connect(gainNode);

        // Aggressive vibrato
        const lfo = this.audioContext.createOscillator();
        const lfoGain = this.audioContext.createGain();
        lfo.type = 'sine';
        lfo.frequency.value = 6;
        lfoGain.gain.value = 5;
        lfo.connect(lfoGain);
        lfoGain.connect(osc1.frequency);
        lfoGain.connect(osc4.frequency);

        oscillators.push(osc1, osc2, osc3, osc4, lfo);
    }

    // Default guitar hold - classic sustained guitar tone
    _createGuitarHold(baseFreq, oscillators, gainNode) {
        // Main tone
        const osc1 = this.audioContext.createOscillator();
        osc1.type = 'sawtooth';
        osc1.frequency.value = baseFreq;

        // Slight detune for richness
        const osc2 = this.audioContext.createOscillator();
        osc2.type = 'sawtooth';
        osc2.frequency.value = baseFreq * 1.002;

        // Octave up for brightness
        const osc3 = this.audioContext.createOscillator();
        osc3.type = 'triangle';
        osc3.frequency.value = baseFreq * 2;

        const gain1 = this.audioContext.createGain();
        const gain2 = this.audioContext.createGain();
        const gain3 = this.audioContext.createGain();
        gain1.gain.value = 0.4;
        gain2.gain.value = 0.3;
        gain3.gain.value = 0.15;

        osc1.connect(gain1);
        osc2.connect(gain2);
        osc3.connect(gain3);
        gain1.connect(gainNode);
        gain2.connect(gainNode);
        gain3.connect(gainNode);

        // Subtle vibrato
        const lfo = this.audioContext.createOscillator();
        const lfoGain = this.audioContext.createGain();
        lfo.type = 'sine';
        lfo.frequency.value = 5;
        lfoGain.gain.value = 3;
        lfo.connect(lfoGain);
        lfoGain.connect(osc1.frequency);
        lfoGain.connect(osc2.frequency);

        oscillators.push(osc1, osc2, osc3, lfo);
    }

    // Create distortion curve for power chord sustain
    _makeDistortionCurve(amount) {
        const samples = 44100;
        const curve = new Float32Array(samples);
        const deg = Math.PI / 180;
        for (let i = 0; i < samples; i++) {
            const x = (i * 2) / samples - 1;
            curve[i] = ((3 + amount) * x * 20 * deg) / (Math.PI + amount * Math.abs(x));
        }
        return curve;
    }

    // Stop the sustained tone for a lane
    stopHoldSustain(lane) {
        const sustain = this.holdSustains.get(lane);
        if (!sustain) return;

        const { oscillators, gainNode } = sustain;
        
        // Quick fade out to avoid clicks
        const now = this.audioContext.currentTime;
        gainNode.gain.setValueAtTime(gainNode.gain.value, now);
        gainNode.gain.linearRampToValueAtTime(0, now + 0.05);

        // Stop oscillators after fade out
        setTimeout(() => {
            oscillators.forEach(osc => {
                try { osc.stop(); } catch (e) {}
            });
        }, 60);

        this.holdSustains.delete(lane);
    }

    // Stop all hold sustains (for pause/end)
    stopAllHoldSustains() {
        for (const lane of this.holdSustains.keys()) {
            this.stopHoldSustain(lane);
        }
    }
}

// Singleton instance
window.blazorHeroAudio = new AudioEngine();

// Interop functions
export async function initAudio() {
    await window.blazorHeroAudio.initialize();
    return true;
}

export async function ensureAudioResumed() {
    await window.blazorHeroAudio.ensureResumed();
}

export async function loadSong(url) {
    return await window.blazorHeroAudio.loadSong(url);
}

export async function loadSfx(name, url) {
    return await window.blazorHeroAudio.loadSfx(name, url);
}

export async function playSong(offsetMs) {
    await window.blazorHeroAudio.playSong(offsetMs || 0);
}

export function pauseSong() {
    window.blazorHeroAudio.pauseSong();
}

export async function resumeSong() {
    await window.blazorHeroAudio.resumeSong();
}

export function stopSong() {
    window.blazorHeroAudio.stopSong();
}

export function getCurrentTime() {
    return window.blazorHeroAudio.getCurrentTime();
}

export function getSongDuration() {
    return window.blazorHeroAudio.getSongDuration();
}

export function isPlaying() {
    return window.blazorHeroAudio.isPlaying;
}

export function playSfx(name, volume) {
    window.blazorHeroAudio.playSfx(name, volume || 1.0);
}

export function setVolume(value) {
    window.blazorHeroAudio.setVolume(value);
}

export function setSfxVolume(value) {
    window.blazorHeroAudio.setSfxVolume(value);
}

export function startHoldSustain(lane, volume) {
    window.blazorHeroAudio.startHoldSustain(lane, volume || 0.4);
}

export function stopHoldSustain(lane) {
    window.blazorHeroAudio.stopHoldSustain(lane);
}

export function stopAllHoldSustains() {
    window.blazorHeroAudio.stopAllHoldSustains();
}

export function loadSongSounds(noteSoundsConfig) {
    window.blazorHeroAudio.loadSongSounds(noteSoundsConfig);
}

export function clearSongSounds() {
    window.blazorHeroAudio.clearSongSounds();
}
