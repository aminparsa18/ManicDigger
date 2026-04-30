using ManicDigger;
using OpenTK.Mathematics;
using static ManicDigger.AudioOpenAl;

public class AudioService : IAudioService
{
    private AudioOpenAl audio;
    public IGameExit gameexit;

    public AudioService(IGameExit gameExit)
    {
        this.gameexit = gameExit;
    }

    private void StartAudio()
    {
        audio ??= new AudioOpenAl(gameexit);
    }

    public AudioData AudioDataCreate(byte[] data, int dataLength)
    {
        StartAudio();
        return GetSampleFromArray(data);
    }

    public bool AudioDataLoaded(AudioData data)
    {
        return true;
    }

    public AudioTask AudioCreate(AudioData data)
    {
        return audio.CreateAudio(data);
    }

    public void AudioPlay(AudioTask audio_)
    {
        StartAudio();
        audio_.Play();
    }

    public void AudioPause(AudioTask audio_)
    {
        audio_.Pause();
    }

    public void AudioDelete(AudioTask audio_)
    {
        audio_.Stop();
    }

    public bool AudioFinished(AudioTask audio_)
    {
        return audio_.Finished;
    }

    public void AudioSetPosition(AudioTask audio_, float x, float y, float z)
    {
        audio_.position = new Vector3(x, y, z);
    }

    public void AudioUpdateListener(float posX, float posY, float posZ, float orientX, float orientY, float orientZ)
    {
        StartAudio();
        UpdateListener(new Vector3(posX, posY, posZ), new Vector3(orientX, orientY, orientZ));
    }
}