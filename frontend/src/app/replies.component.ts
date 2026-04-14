import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface AttachmentItem {
  originalFileName: string;
  storedFileName: string;
  contentType: string;
  type: string;
}

export interface CommentItem {
  id: string;
  userName: string;
  email: string;
  homePage?: string;
  text: string;
  createdAtUtc: string;
  parentCommentId?: string;
  attachments: AttachmentItem[];
  replies: CommentItem[];
}

@Component({
  selector: 'app-replies',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngFor="let item of items" class="reply-item">
      <div class="comment-meta">
        <strong>{{ item.userName }}</strong>
        <span class="sep">·</span>
        <a *ngIf="item.homePage" [href]="item.homePage" target="_blank" rel="noopener">{{ item.email }}</a>
        <span *ngIf="!item.homePage">{{ item.email }}</span>
        <span class="sep">·</span>
        <span class="date">{{ item.createdAtUtc | date:'dd MMM yyyy, HH:mm' }}</span>
        <button class="btn-reply-small" (click)="replyTo.emit(item)">Reply</button>
      </div>
      <div class="comment-text" [innerHTML]="item.text"></div>
      <div *ngIf="item.attachments.length" class="attachments">
        <ng-container *ngFor="let att of item.attachments">
          <ng-container *ngIf="att.type === 'Image'">
            <img
              class="att-thumb"
              [src]="apiBase + '/uploads/' + att.storedFileName"
              [alt]="att.originalFileName"
              (click)="openImageLightbox(apiBase + '/uploads/' + att.storedFileName, att.originalFileName)"
              title="{{ att.originalFileName }}" />
          </ng-container>
          <ng-container *ngIf="att.type === 'Text'">
            <button class="att-text-btn" (click)="openTextLightbox(att)">
              📄 {{ att.originalFileName }}
            </button>
          </ng-container>
        </ng-container>
      </div>
      <div class="replies" *ngIf="item.replies.length">
        <app-replies [items]="item.replies" [apiBase]="apiBase" (replyTo)="replyTo.emit($event)"></app-replies>
      </div>
    </div>

    <!-- Image lightbox -->
    <div *ngIf="imageLightboxUrl" class="lightbox-overlay" (click)="closeImageLightbox()">
      <div class="lightbox-box" (click)="$event.stopPropagation()">
        <button class="lightbox-close" (click)="closeImageLightbox()">✕</button>
        <img [src]="imageLightboxUrl" [alt]="imageLightboxAlt" class="lightbox-img" />
        <div class="lightbox-caption">{{ imageLightboxAlt }}</div>
      </div>
    </div>

    <!-- Text lightbox -->
    <div *ngIf="textLightboxOpen" class="lightbox-overlay" (click)="closeTextLightbox()">
      <div class="lightbox-box lightbox-text-box" (click)="$event.stopPropagation()">
        <button class="lightbox-close" (click)="closeTextLightbox()">✕</button>
        <div class="lightbox-text-header">📄 {{ textLightboxName }}</div>
        <div *ngIf="textLightboxLoading" class="lightbox-loading">Loading…</div>
        <pre *ngIf="!textLightboxLoading" class="lightbox-text-content">{{ textLightboxContent }}</pre>
      </div>
    </div>
  `,
  styles: [`
    .reply-item { border-left: 2px solid #dde2f0; padding: 8px 0 4px 12px; margin-top: 8px; }
    .comment-meta { font-size: 0.85rem; color: #555; display: flex; align-items: center; flex-wrap: wrap; gap: 4px; margin-bottom: 4px; }
    .sep { color: #bbb; }
    .date { color: #888; }
    .btn-reply-small { margin-left: 8px; font-size: 0.78rem; background: #e8eaf6; border: none; border-radius: 6px; padding: 2px 8px; cursor: pointer; color: #3247ff; }
    .btn-reply-small:hover { background: #c5caff; }
    .comment-text { margin: 4px 0; line-height: 1.5; }
    .attachments { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 8px; }
    .att-thumb { max-width: 160px; max-height: 120px; border-radius: 6px; cursor: pointer; border: 2px solid transparent; transition: border-color .2s, transform .2s; object-fit: cover; }
    .att-thumb:hover { border-color: #3247ff; transform: scale(1.03); }
    .att-text-btn { font-size: 0.85rem; color: #3247ff; background: #f0f2ff; border: 1px solid #c5cae9; border-radius: 6px; padding: 4px 12px; cursor: pointer; transition: background .15s, transform .15s; }
    .att-text-btn:hover { background: #dde2ff; transform: translateY(-1px); }
    .replies { margin-left: 8px; }
    /* shared lightbox */
    .lightbox-overlay { position: fixed; inset: 0; background: rgba(0,0,0,.75); display: flex; align-items: center; justify-content: center; z-index: 9999; animation: fadeIn .2s; }
    .lightbox-box { position: relative; max-width: 90vw; max-height: 90vh; background: white; border-radius: 12px; padding: 16px; box-shadow: 0 8px 40px rgba(0,0,0,.4); animation: zoomIn .2s; }
    .lightbox-close { position: absolute; top: 8px; right: 10px; background: none; border: none; font-size: 1.4rem; cursor: pointer; color: #555; }
    .lightbox-img { max-width: 80vw; max-height: 75vh; display: block; border-radius: 8px; }
    .lightbox-caption { text-align: center; font-size: 0.85rem; color: #666; margin-top: 8px; }
    /* text lightbox extras */
    .lightbox-text-box { width: 70vw; max-height: 80vh; display: flex; flex-direction: column; }
    .lightbox-text-header { font-weight: 600; margin-bottom: 10px; padding-top: 4px; color: #1a237e; }
    .lightbox-loading { color: #888; font-style: italic; }
    .lightbox-text-content { flex: 1; overflow: auto; background: #f8f9fc; border-radius: 8px; padding: 12px; font-size: 0.88rem; line-height: 1.6; white-space: pre-wrap; word-break: break-word; margin: 0; max-height: 65vh; border: 1px solid #e0e3ef; }
    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
    @keyframes zoomIn { from { transform: scale(.85); opacity: 0; } to { transform: scale(1); opacity: 1; } }
  `]
})
export class RepliesComponent {
  @Input() items: CommentItem[] = [];
  @Input() apiBase = 'http://localhost:8080';
  @Output() replyTo = new EventEmitter<CommentItem>();

  // image lightbox
  imageLightboxUrl = '';
  imageLightboxAlt = '';

  openImageLightbox(url: string, alt: string): void {
    this.imageLightboxUrl = url;
    this.imageLightboxAlt = alt;
  }
  closeImageLightbox(): void {
    this.imageLightboxUrl = '';
    this.imageLightboxAlt = '';
  }

  // text lightbox
  textLightboxOpen = false;
  textLightboxName = '';
  textLightboxContent = '';
  textLightboxLoading = false;

  async openTextLightbox(att: AttachmentItem): Promise<void> {
    this.textLightboxOpen = true;
    this.textLightboxName = att.originalFileName;
    this.textLightboxContent = '';
    this.textLightboxLoading = true;
    try {
      const res = await fetch(`${this.apiBase}/api/attachments/${att.storedFileName}`);
      this.textLightboxContent = res.ok ? await res.text() : 'Failed to load file.';
    } catch {
      this.textLightboxContent = 'Failed to load file.';
    } finally {
      this.textLightboxLoading = false;
    }
  }
  closeTextLightbox(): void {
    this.textLightboxOpen = false;
    this.textLightboxName = '';
    this.textLightboxContent = '';
  }
}
