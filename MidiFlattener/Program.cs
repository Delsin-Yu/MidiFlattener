using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

var targetTempoValue = 140;
const string extension = ".flattened.mid";

var dir = args[0];

foreach (var file in Directory.GetFiles(dir, "*.mid", SearchOption.AllDirectories))
{
    if(file.Contains(extension)) continue;
    
    var sourceMidiFile = MidiFile.Read(file);

    var sourceMidiTrack =
        sourceMidiFile
            .Chunks
            .OfType<TrackChunk>()
            .First(track => track.GetNotes().Count != 0);

    var targetTempo = Tempo.FromBeatsPerMinute(targetTempoValue);
    var dstTempoMap = TempoMap.Create(targetTempo);
    
    var sourceTempoMap = sourceMidiFile.GetTempoMap();
    var noteTime = sourceMidiTrack
        .GetNotes()
        .Select(x => (
            x.NoteName,
            x.Octave,
            x.Velocity,
            x.OffVelocity,
            Time: x.TimeAs<MetricTimeSpan>(sourceTempoMap),
            Length: x.LengthAs<MetricTimeSpan>(sourceTempoMap))
        ).ToList();

    var timedEvents = sourceMidiTrack
        .GetTimedEvents()
        .Where(x => x.Event is ControlChangeEvent)
        .Select(
            x =>
            {
                var cc = (ControlChangeEvent)x.Event;
                return (
                    cc.ControlNumber,
                    cc.ControlValue,
                    Time: x.TimeAs<MetricTimeSpan>(sourceTempoMap)
                );
            });
    
    var midiTrack = new TrackChunk();
    
    var newMidi = new MidiFile(
        new TrackChunk(
            new TimeSignatureEvent(4, 4),
            new SetTempoEvent(targetTempo.MicrosecondsPerQuarterNote)
        ),
        midiTrack
    );

    var noteManager = midiTrack.ManageNotes();
    foreach (var data in noteTime)
    {
        var time = TimeConverter.ConvertFrom(data.Time, dstTempoMap);
        var length = TimeConverter.ConvertFrom(data.Length, dstTempoMap);
        
        var note = new Note(
            data.NoteName,
            data.Octave,
            length,
            time
        )
        {
            Velocity = data.Velocity,
            OffVelocity = data.OffVelocity
        };

        noteManager.Objects.Add(note);
    }
    noteManager.SaveChanges();

    var eventManager = midiTrack.ManageTimedEvents();
    foreach (var data in timedEvents)
    {
        var time = TimeConverter.ConvertFrom(data.Time, dstTempoMap);
        var cc = new ControlChangeEvent(data.ControlNumber, data.ControlValue);
        eventManager.Objects.Add(new TimedEvent(cc, time));
    }
    eventManager.SaveChanges();
    
    newMidi.Write(Path.ChangeExtension(file, extension), true);
}