using static ManicDigger.AudioOpenAl;
// ─────────────────────────────────────────────────────────────────────────────
// Audio
// ─────────────────────────────────────────────────────────────────────────────

public interface IAudioService
{
    AudioData AudioDataCreate(byte[] data, int dataLength);
    bool AudioDataLoaded(AudioData data);
    AudioTask AudioCreate(AudioData data);
    void AudioPlay(AudioTask audio);
    void AudioPause(AudioTask audio);
    void AudioDelete(AudioTask audio);
    bool AudioFinished(AudioTask audio);
    void AudioSetPosition(AudioTask audio, float x, float y, float z);
    void AudioUpdateListener(float posX, float posY, float posZ, float orientX, float orientY, float orientZ);
}

