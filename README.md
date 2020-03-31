# Lyrics Animator

This project creates MP4 movies with scrolling lyrics synced up to the audio file you provide.

It's not automatic - you have to write out the lyrics, provide the start and end times for each verse, and of course provide the audio file, but the program does the rest and you end up with a nice lyrics movie.

If that sounds like something you'd like, [download the latest release](https://github.com/mwinckler/LyricsAnimator/releases)!

You will also need to [download FFMPEG](https://ffmpeg.org/download.html). Note the location where you save ffmpeg.exe - you'll need to enter that path when you run the lyrics animator so it knows where to find it.

## Generating lyrics videos

1. Fill out a configuration JSON file for each song you want to generate a lyrics video for. There's [an example configuration file](./examples/example_config.json) in the `examples` directory of this repository. Within the configuration file:
    1. `songTitle` will be displayed at the top of the video.
    2. `audioFilePath` is the full path to the location of your song audio file.
    3. `outputFilename` is the filename of the MP4 movie which will be written to the output directory.
    4. `lyrics` is a list of lyric objects. Each of these has the following properties:
        * `startTime` (HH:MM:SS) - when does this verse start? (Use the actual start time of the verse in the audio file - the program will arrange to have the text at the right spot when the singing starts.)
        * `endTime` (HH:MM:SS) - when does this verse end?
        * `lines` - this is an array of strings containing your actual lyrics. You probably want these to broken out by lines in the song, but there's no hard-and-fast requirement. Blank lines will be inserted between each string in the final video.
        * `verseNumber` (int) - this is optional, but if you provide a number greater than 0 here, a "verse #" label will be added to the lower right of the video.
2. Run `LyricsAnimator.exe`. On the main screen, provide:
    * Path to ffmpeg: the path to the `ffmpeg.exe` executable on your system (the one you downloaded above)
    * Config directory: the directory containing your configuration .json files
    * Output directory: where you want the lyrics movies to be saved
3. Click "Create lyric videos" and wait!

## License

This project is released under [the MIT License](./LICENSE). Please feel free to use and modify it.