import json
import os
import sys
import traceback


def configure_stdio():
    streams = (
        (sys.stdin, "utf-8-sig"),
        (sys.stdout, "utf-8"),
        (sys.stderr, "utf-8"),
    )
    for stream, encoding in streams:
        try:
            stream.reconfigure(encoding=encoding, errors="replace")
        except (AttributeError, OSError):
            pass


configure_stdio()

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
SUPPORTED_MODELS = ("tiny", "base", "small", "medium", "large-v3")


def respond(payload):
    print(json.dumps(payload, ensure_ascii=False, separators=(",", ":")), flush=True)


def get_model(model_name, cache_dir):
    global _model, _model_key

    if model_name not in SUPPORTED_MODELS:
        raise ValueError(f"Unsupported model: {model_name}")

    if IMPORT_ERROR is not None:
        raise RuntimeError(
            "faster-whisper is not available in this runtime. "
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
                "faster-whisper is not available in this runtime. "
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
            # Some launchers may prepend a UTF-8 BOM to the first request.
            line = line.lstrip("\ufeff").strip()
            if not line:
                continue
            request = json.loads(line)
            response = handle(request)
            respond(response)
            if response.get("shutdown"):
                return
        except Exception as exc:
            print(traceback.format_exc(), file=sys.stderr, flush=True)
            respond({"ok": False, "error": str(exc)})


def download_model(model_name, cache_dir):
    cache_dir = os.path.abspath(cache_dir)
    get_model(model_name, cache_dir)
    respond({"ok": True, "model": model_name, "cache_dir": cache_dir})


def run_command_line():
    if len(sys.argv) == 4 and sys.argv[1] == "--download-model":
        try:
            download_model(sys.argv[2], sys.argv[3])
            return 0
        except Exception as exc:
            print(traceback.format_exc(), file=sys.stderr, flush=True)
            respond({"ok": False, "error": str(exc)})
            return 1

    print(
        "Usage: faster-whisper-worker.exe --download-model "
        "<tiny|base|small|medium|large-v3> <cache-directory>",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise SystemExit(run_command_line())
    main()
