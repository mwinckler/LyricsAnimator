# Lyrics Animator

This project creates MP4 movies with scrolling lyrics synced up to the audio file you provide.

It's not automatic - you have to write out the lyrics, provide the start and end times for each verse, and of course provide the audio file, but the program does the rest and you end up with a nice lyrics movie.

If that sounds like something you'd like, [download the latest release](https://github.com/mwinckler/LyricsAnimator/releases)!

You will also need to [download FFMPEG](https://ffmpeg.org/download.html). Note the location where you save ffmpeg.exe - you'll need to enter that path when you run the lyrics animator so it knows where to find it.

## One-time configuration

Create an app configuration by either editing [the example config.json](./examples/config.json) and saving it in the same directory as the executable, or run `LyricsAnimator.exe` and fill in the configuration boxes there (which will also save a `config.json`) file next to the executable. In particular, you must specify the path to `ffmpeg.exe`.

## Generating lyrics videos

1. Create a lyrics file for each song you want to generate a video for. A lyrics file has the following structure:
    * The name of the file must match the name of the audio file, but with a `.txt` extension instead of `.mp3`. For example, if you are creating lyrics for the file `370_A_Hymn_of_Glory.mp3`, then your lyrics file must be named `370_A_Hymn_of_Glory.txt`.
    * The first line in the file must be the title, which will be displayed at the top of the video.
    * The second line in the file must be blank.
    * Each subsequent line will be displayed as a "paragraph" in the lyrics. Each line is separated by whitespace in the final output.
    * Any lyric line may start with a timestamp in the format `[HH:MM:SS]`, for example `[00:01:23]`. This is the timestamp when that line should be displayed at a readable position in the output video.
    * A lyric line may also be preceded by a "verse text" designator in the format `{ verse X }`. Text inside the curly braces is displayed alongside that line in the right margin of the video.
2. If you want a graphical UI, run `LyricsAnimator.exe` and provide:
    * Path to ffmpeg: the path to the `ffmpeg.exe` executable on your system (the one you downloaded above)
    * Config directory: the directory containing your configuration .json files
    * Output directory: where you want the lyrics movies to be saved
3. To generate videos from the command line, run `animator.exe --songConfigDir "C:\path\to\your\lyric\and\audio\files"`. You can also run `animator.exe --help` to see all options.

## License

This project is released under [the MIT License](./LICENSE). Please feel free to use and modify it.