"use client";
import { useRef, useState } from "react";
import { cn } from "@/lib/utils";

interface FileUploadProps {
  onFileSelect: (file: File | null) => void;
  accept?: string;
  maxSizeMb?: number;
  className?: string;
}

export function FileUpload({ onFileSelect, accept, maxSizeMb = 50, className }: FileUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [selected, setSelected] = useState<File | null>(null);
  const [error, setError] = useState("");
  const [dragging, setDragging] = useState(false);

  function handleFile(file: File | null) {
    setError("");
    if (!file) { setSelected(null); onFileSelect(null); return; }
    if (file.size > maxSizeMb * 1024 * 1024) {
      setError(`File exceeds ${maxSizeMb} MB limit`);
      return;
    }
    setSelected(file);
    onFileSelect(file);
  }

  return (
    <div className={cn("space-y-2", className)}>
      <div
        onClick={() => inputRef.current?.click()}
        onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => { e.preventDefault(); setDragging(false); handleFile(e.dataTransfer.files[0] ?? null); }}
        className={cn(
          "border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-colors",
          dragging ? "border-primary bg-primary-50" : "border-border hover:border-text-muted"
        )}
      >
        <div className="text-3xl mb-2">📎</div>
        {selected ? (
          <div>
            <p className="text-sm font-medium text-text-primary">{selected.name}</p>
            <p className="text-xs text-text-secondary mt-1">{(selected.size / 1024 / 1024).toFixed(2)} MB</p>
          </div>
        ) : (
          <div>
            <p className="text-sm font-medium text-text-primary">Drop file here or click to browse</p>
            <p className="text-xs text-text-muted mt-1">Max {maxSizeMb} MB</p>
          </div>
        )}
        <input ref={inputRef} type="file" accept={accept} className="hidden"
          onChange={(e) => handleFile(e.target.files?.[0] ?? null)} />
      </div>
      {selected && (
        <button type="button" onClick={() => handleFile(null)}
          className="text-xs text-danger-500 hover:text-danger-700">Remove file</button>
      )}
      {error && <p className="text-xs text-danger-700">{error}</p>}
    </div>
  );
}
