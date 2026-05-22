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
          dragging ? "border-blue-400 bg-blue-50" : "border-gray-300 hover:border-gray-400"
        )}
      >
        <div className="text-3xl mb-2">📎</div>
        {selected ? (
          <div>
            <p className="text-sm font-medium text-gray-900">{selected.name}</p>
            <p className="text-xs text-gray-500 mt-1">{(selected.size / 1024 / 1024).toFixed(2)} MB</p>
          </div>
        ) : (
          <div>
            <p className="text-sm font-medium text-gray-700">Drop file here or click to browse</p>
            <p className="text-xs text-gray-400 mt-1">Max {maxSizeMb} MB</p>
          </div>
        )}
        <input ref={inputRef} type="file" accept={accept} className="hidden"
          onChange={(e) => handleFile(e.target.files?.[0] ?? null)} />
      </div>
      {selected && (
        <button type="button" onClick={() => handleFile(null)}
          className="text-xs text-red-500 hover:text-red-700">Remove file</button>
      )}
      {error && <p className="text-xs text-red-600">{error}</p>}
    </div>
  );
}
