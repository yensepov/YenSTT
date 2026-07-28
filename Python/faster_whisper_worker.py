import json
import os
import sys
import traceback

try:
    from faster_whisper import WhisperModel
    from importlib.metadata import version

    FASTER_WHISPER_VERSION = version("faster-whisper")
    IMPORT_ERROR = None
except Exception as exc:  # Keep the protocol alive to return a useful error.
    WhisperModel = None
    FASTER_WHISPER_VERSION = None
    IMPORT_ERROR = exc


_model = None
_model_key = None


def respond(payload):
    print(json.dumps(payload, ensure_ascii=False, separators=(",", ":")), flush=True)


def get_model(model_name, cache_dir):
    global _model, _model_key

    if IMPORT_ERROR is not None:
        raise RuntimeError(
            "faster-whisper is not installed. Run install-local-whisper.bat. "
            f"Details: {IMPORT_ERROR}"
        )

    key = (model_name, cache_dir)
    if _model is None or _model_key != key:
        os.makedirs(cache_dir, exist_ok=True)
        print(f"Loading faster-whisper model '{model_name}' on CPU (int8)...", file=sys.stderr, flush=True)
        _model = WhisperModel(
            model_name,
            device="cpu",
            compute_type="int8",
            download_root=cache_dir,
        )
        _model_key = key
        print("Local Whisper model is ready.", file=sys.stderr, flush=True)

    return _model


def transcribe(request):
    audio_path = request["audio_path"]
    if not os.path.isfile(audio_path):
        raise FileNotFoundError(f"Audio file not found: {audio_path}")

    model_name = request.get("model") or "small"
    cache_dir = request["cache_dir"]
    language = request.get("language") or None
    prompt = request.get("prompt") or None
    model = get_model(model_name, cache_dir)

    segments, info = model.transcribe(
        audio_path,
        language=language,
        initial_prompt=prompt,
        beam_size=5,
        vad_filter=True,
        condition_on_previous_text=False,
    )
    text = "".join(segment.text for segment in segments).strip()
    return {
        "ok": True,
        "text": text,
        "language": info.language,
        "duration": info.duration,
    }


def handle(request):
    command = request.get("command")
    if command == "ping":
        if IMPORT_ERROR is not None:
            raise RuntimeError(
                "faster-whisper is not installed. Run install-local-whisper.bat. "
                f"Details: {IMPORT_ERROR}"
            )
        return {"ok": True, "version": FASTER_WHISPER_VERSION}
    if command == "transcribe":
        return transcribe(request)
    if command == "shutdown":
        return {"ok": True, "shutdown": True}
    raise ValueError(f"Unknown command: {command}")


def main():
    for line in sys.stdin:
        try:
            request = json.loads(line)
            response = handle(request)
            respond(response)
            if response.get("shutdown"):
                return
        except Exception as exc:
            print(traceback.format_exc(), file=sys.stderr, flush=True)
            respond({"ok": False, "error": str(exc)})


if __name__ == "__main__":
    main()
