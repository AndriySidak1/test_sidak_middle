import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import * as signalR from '@microsoft/signalr';
import { CommentItem, RepliesComponent } from './replies.component';

const API_BASE = 'http://localhost:8080';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RepliesComponent],
  template: `
    <main class="container">
      <h1 class="app-title">Comments SPA</h1>

      <!-- NEW COMMENT FORM -->
      <section class="card">
        <h2>{{ replyTarget ? 'Reply to ' + replyTarget.userName : 'Add comment' }}</h2>
        <div *ngIf="replyTarget" class="reply-banner">
          Replying to: <em>{{ replyTarget.text | slice:0:80 }}</em>
          <button class="btn-cancel-reply" (click)="cancelReply()">Cancel</button>
        </div>

        <form [formGroup]="form" class="form" (ngSubmit)="submit()">
          <div class="field">
            <label>User Name <span class="req">*</span></label>
            <input formControlName="userName" placeholder="Alphanumeric only" />
            <span class="err" *ngIf="touched('userName') && ctrl('userName').hasError('required')">Required.</span>
            <span class="err" *ngIf="touched('userName') && ctrl('userName').hasError('pattern')">Only letters and digits allowed.</span>
          </div>

          <div class="field">
            <label>E-mail <span class="req">*</span></label>
            <input formControlName="email" type="email" placeholder="user@example.com" />
            <span class="err" *ngIf="touched('email') && ctrl('email').hasError('required')">Required.</span>
            <span class="err" *ngIf="touched('email') && ctrl('email').hasError('email')">Invalid e-mail format.</span>
          </div>

          <div class="field">
            <label>Home page</label>
            <input formControlName="homePage" placeholder="https://example.com (optional)" />
            <span class="err" *ngIf="touched('homePage') && ctrl('homePage').hasError('pattern')">Must be a valid URL.</span>
          </div>

          <div class="field">
            <label>Text <span class="req">*</span></label>
            <div class="toolbar">
              <button type="button" class="toolbar-btn" (click)="wrapTag('i')" title="Italic">[i]</button>
              <button type="button" class="toolbar-btn" (click)="wrapTag('strong')" title="Bold">[strong]</button>
              <button type="button" class="toolbar-btn" (click)="wrapTag('code')" title="Code">[code]</button>
              <button type="button" class="toolbar-btn" (click)="insertLink()" title="Link">[a]</button>
            </div>
            <textarea #messageBox rows="6" formControlName="text" placeholder="Your message..."></textarea>
            <span class="err" *ngIf="touched('text') && ctrl('text').hasError('required')">Required.</span>
          </div>

          <div class="field">
            <label>Attachment <span class="hint">(image JPG/GIF/PNG max 320×240 or TXT max 100 KB)</span></label>
            <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap">
              <input #fileInput type="file" (change)="onFileSelected($event)" accept=".jpg,.jpeg,.gif,.png,.txt" style="flex:1;min-width:0" />
              @if (selectedFile) {
                <span class="file-info">{{ selectedFile.name }}</span>
                <button type="button" class="btn-remove-file" (click)="clearFile(fileInput)" title="Remove attachment">✕</button>
              }
            </div>
          </div>

          <div class="field captcha-field">
            <label>CAPTCHA <span class="req">*</span></label>
            <div class="captcha-row">
              <img *ngIf="captchaImageBase64"
                   [src]="'data:image/svg+xml;base64,' + captchaImageBase64"
                   alt="captcha"
                   class="captcha-img" />
              <button type="button" class="btn-refresh" (click)="loadCaptcha()" title="Refresh">↺</button>
            </div>
            <input formControlName="captchaCode" placeholder="Enter characters shown above" />
            <span class="err" *ngIf="touched('captchaCode') && ctrl('captchaCode').hasError('required')">Required.</span>
            <span class="err" *ngIf="touched('captchaCode') && ctrl('captchaCode').hasError('pattern')">Only letters and digits.</span>
          </div>

          <div *ngIf="serverError" class="server-error">{{ serverError }}</div>

          <div class="actions">
            <button type="button" class="btn-secondary" (click)="preview()">Preview</button>
            <button type="submit" class="btn-primary" [disabled]="loading">
              <span *ngIf="loading" class="spinner"></span>
              {{ loading ? 'Submitting…' : (replyTarget ? 'Post Reply' : 'Post Comment') }}
            </button>
          </div>
        </form>
      </section>

      <!-- PREVIEW -->
      <section class="card preview-card" *ngIf="previewHtml">
        <h2>Preview</h2>
        <div class="preview" [innerHTML]="previewHtml"></div>
      </section>

      <!-- SEARCH -->
      <section class="card">
        <div class="search-row">
          <input class="search-input" [(ngModel)]="searchQuery" [ngModelOptions]="{standalone: true}"
                 placeholder="Search comments (Elasticsearch)…"
                 (keyup.enter)="runSearch()" />
          <button class="btn-primary" (click)="runSearch()">Search</button>
          <button class="btn-secondary" *ngIf="searchResults" (click)="clearSearch()">Clear</button>
        </div>
      </section>

      <!-- SEARCH RESULTS -->
      <section class="card" *ngIf="searchResults">
        <h2>Search results ({{ searchResults.length }})</h2>
        <div *ngIf="searchResults.length === 0" class="empty">No results found.</div>
        <div *ngFor="let r of searchResults" class="search-result">
          <strong>{{ r.userName }}</strong> · {{ r.email }}
          <div [innerHTML]="r.text | slice:0:200"></div>
        </div>
      </section>

      <!-- COMMENTS TABLE -->
      <section class="card">
        <div class="comments-header">
          <h2>Comments <span class="badge">{{ total }}</span></h2>
          <div class="ws-indicator" [class.connected]="wsConnected" title="Real-time: {{ wsConnected ? 'connected' : 'disconnected' }}">
            ● {{ wsConnected ? 'Live' : 'Offline' }}
          </div>
        </div>

        <div *ngIf="comments.length === 0" class="empty">No comments yet. Be the first!</div>

        <table *ngIf="comments.length > 0" class="comments-table">
          <thead>
            <tr>
              <th class="th-sort" (click)="sortBy === 'userName' ? toggleDir() : setSort('userName')" title="Sort by User Name">
                User Name
                <span class="sort-arrow" [class.active]="sortBy === 'userName'">
                  {{ sortBy === 'userName' ? (sortDirection === 'asc' ? '▲' : '▼') : '⇅' }}
                </span>
              </th>
              <th class="th-sort" (click)="sortBy === 'email' ? toggleDir() : setSort('email')" title="Sort by E-mail">
                E-mail
                <span class="sort-arrow" [class.active]="sortBy === 'email'">
                  {{ sortBy === 'email' ? (sortDirection === 'asc' ? '▲' : '▼') : '⇅' }}
                </span>
              </th>
              <th class="th-sort" (click)="sortBy === 'createdAt' ? toggleDir() : setSort('createdAt')" title="Sort by Date">
                Date
                <span class="sort-arrow" [class.active]="sortBy === 'createdAt'">
                  {{ sortBy === 'createdAt' ? (sortDirection === 'asc' ? '▲' : '▼') : '⇅' }}
                </span>
              </th>
              <th>Message</th>
            </tr>
          </thead>
          <tbody>
            <ng-container *ngFor="let comment of comments">
              <!-- main row -->
              <tr class="comment-main-row" [class.row-highlight]="comment.id === newCommentId">
                <td class="td-user">
                  <a *ngIf="comment.homePage" [href]="comment.homePage" target="_blank" rel="noopener" class="user-link">
                    {{ comment.userName }}
                  </a>
                  <span *ngIf="!comment.homePage">{{ comment.userName }}</span>
                </td>
                <td class="td-email">{{ comment.email }}</td>
                <td class="td-date">{{ comment.createdAtUtc | date:'dd MMM yyyy' }}<br/><span class="time">{{ comment.createdAtUtc | date:'HH:mm' }}</span></td>
                <td class="td-message">
                  <div class="comment-text" [innerHTML]="comment.text"></div>
                  <div *ngIf="comment.attachments.length" class="attachments">
                    <ng-container *ngFor="let att of comment.attachments">
                      <ng-container *ngIf="att.type === 'Image'">
                        <img
                          class="att-thumb"
                          [src]="apiBase + '/uploads/' + att.storedFileName"
                          [alt]="att.originalFileName"
                          (click)="openLightbox(apiBase + '/uploads/' + att.storedFileName, att.originalFileName)"
                          title="{{ att.originalFileName }}" />
                      </ng-container>
                      <ng-container *ngIf="att.type === 'Text'">
                        <button class="att-text-btn" (click)="openTextLightbox(att)">
                          📄 {{ att.originalFileName }}
                        </button>
                      </ng-container>
                    </ng-container>
                  </div>
                  <button class="btn-reply-small" (click)="startReply(comment)">Reply</button>
                </td>
              </tr>
              <!-- replies row (cascade, spans all columns) -->
              <tr *ngIf="comment.replies.length" class="comment-replies-row">
                <td colspan="4" class="td-replies">
                  <app-replies [items]="comment.replies" [apiBase]="apiBase" (replyTo)="startReply($event)"></app-replies>
                </td>
              </tr>
            </ng-container>
          </tbody>
        </table>

        <div class="pagination">
          <button class="btn-page" (click)="changePage(-1)" [disabled]="page <= 1">← Prev</button>
          <span class="page-info">Page {{ page }} / {{ totalPages }}</span>
          <button class="btn-page" (click)="changePage(1)" [disabled]="page >= totalPages">Next →</button>
        </div>
      </section>
    </main>

    <!-- Image lightbox -->
    <div *ngIf="lightboxUrl" class="lightbox-overlay" (click)="closeLightbox()">
      <div class="lightbox-box" (click)="$event.stopPropagation()">
        <button class="lightbox-close" (click)="closeLightbox()">✕</button>
        <img [src]="lightboxUrl" [alt]="lightboxAlt" class="lightbox-img" />
        <div class="lightbox-caption">{{ lightboxAlt }}</div>
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
    .container { max-width: 1000px; margin: 24px auto; padding: 0 16px; }
    .app-title { font-size: 1.8rem; margin-bottom: 16px; color: #1a237e; }
    .card { background: white; border-radius: 12px; padding: 20px; box-shadow: 0 2px 10px rgba(0,0,0,.07); margin-bottom: 16px; }
    h2 { margin: 0 0 12px; font-size: 1.1rem; color: #263238; }
    .form { display: grid; gap: 12px; }
    .field { display: grid; gap: 4px; }
    label { font-size: 0.88rem; font-weight: 600; color: #444; }
    .req { color: #e53935; }
    .hint { font-weight: 400; color: #888; font-size: 0.8rem; }
    input, textarea, select { border: 1px solid #d4d8e0; border-radius: 8px; padding: 8px 10px; font-size: 0.93rem; width: 100%; box-sizing: border-box; transition: border-color .15s; }
    input:focus, textarea:focus, select:focus { outline: none; border-color: #3247ff; }
    .err { font-size: 0.8rem; color: #e53935; }
    .server-error { background: #fdecea; color: #c62828; border-radius: 8px; padding: 8px 12px; font-size: 0.9rem; }
    .toolbar { display: flex; gap: 6px; }
    .toolbar-btn { border: 1px solid #c5cae9; border-radius: 6px; padding: 4px 10px; background: #f3f4ff; cursor: pointer; font-size: 0.85rem; color: #3247ff; }
    .toolbar-btn:hover { background: #c5caff; }
    .captcha-row { display: flex; align-items: center; gap: 8px; margin-bottom: 4px; }
    .captcha-img { border-radius: 6px; border: 1px solid #e0e0e0; }
    .btn-refresh { border: 1px solid #c5cae9; background: #f3f4ff; border-radius: 6px; padding: 4px 10px; cursor: pointer; font-size: 1.1rem; color: #3247ff; }
    .file-info { font-size: 0.82rem; color: #555; }
    .btn-remove-file { background: none; border: 1px solid #ccc; border-radius: 4px; padding: 2px 7px; cursor: pointer; color: #c00; font-size: 0.85rem; line-height: 1.4; }
    .btn-remove-file:hover { background: #fee; border-color: #c00; }
    .actions { display: flex; gap: 10px; margin-top: 4px; }
    .btn-primary { background: #3247ff; color: white; border: none; border-radius: 8px; padding: 9px 18px; cursor: pointer; font-size: 0.93rem; }
    .btn-primary:hover { background: #1a32e8; }
    .btn-primary:disabled { background: #9fa8da; cursor: not-allowed; }
    .btn-secondary { background: #e8eaf6; color: #3247ff; border: none; border-radius: 8px; padding: 9px 16px; cursor: pointer; font-size: 0.93rem; }
    .btn-secondary:hover { background: #c5caff; }
    .preview { min-height: 60px; border: 1px dashed #c3cada; border-radius: 8px; padding: 10px; line-height: 1.6; }
    .search-row { display: flex; gap: 8px; align-items: center; }
    .search-input { flex: 1; }
    .search-result { border-top: 1px solid #eceff8; padding: 8px 0; }
    .comments-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .badge { background: #e8eaf6; color: #3247ff; border-radius: 99px; padding: 1px 9px; font-size: 0.82rem; font-weight: 700; margin-left: 6px; }
    .ws-indicator { font-size: 0.82rem; color: #e53935; }
    .ws-indicator.connected { color: #2e7d32; }
    .empty { color: #999; font-style: italic; padding: 8px 0; }
    /* ---- comments table ---- */
    .comments-table { width: 100%; border-collapse: collapse; font-size: 0.93rem; }
    .comments-table thead tr { background: #e8eaf6; }
    .th-sort { padding: 10px 14px; text-align: left; cursor: pointer; user-select: none; white-space: nowrap; font-weight: 700; color: #1a237e; transition: background .15s; }
    .th-sort:hover { background: #c5caff; }
    .comments-table th:last-child { width: 60%; padding: 10px 14px; text-align: left; font-weight: 700; color: #1a237e; }
    .sort-arrow { margin-left: 4px; font-size: 0.78rem; color: #9fa8da; }
    .sort-arrow.active { color: #3247ff; }
    .comment-main-row td { padding: 10px 14px; vertical-align: top; border-bottom: 1px solid #eceff8; }
    .comment-main-row:hover td { background: #f7f8ff; }
    .comment-replies-row td { padding: 0 14px 10px 14px; border-bottom: 1px solid #eceff8; background: #fbfcff; }
    .td-user { font-weight: 600; white-space: nowrap; }
    .td-email { color: #555; white-space: nowrap; }
    .td-date { color: #666; white-space: nowrap; font-size: 0.85rem; }
    .time { color: #aaa; font-size: 0.8rem; }
    .user-link { color: #3247ff; text-decoration: none; }
    .user-link:hover { text-decoration: underline; }
    .td-replies { padding-top: 0; }
    .comment-text { line-height: 1.55; margin: 0 0 6px; }
    .attachments { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 8px; }
    .att-thumb { max-width: 160px; max-height: 120px; border-radius: 6px; cursor: pointer; border: 2px solid transparent; transition: border-color .2s, transform .2s; object-fit: cover; }
    .att-thumb:hover { border-color: #3247ff; transform: scale(1.04); }
    .att-text-btn { font-size: 0.85rem; color: #3247ff; background: #f0f2ff; border: 1px solid #c5cae9; border-radius: 6px; padding: 4px 12px; cursor: pointer; transition: background .15s, transform .15s; }
    .att-text-btn:hover { background: #dde2ff; transform: translateY(-1px); }
    .lightbox-text-box { width: 70vw; max-height: 80vh; display: flex; flex-direction: column; }
    .lightbox-text-header { font-weight: 600; margin-bottom: 10px; padding-top: 4px; color: #1a237e; }
    .lightbox-loading { color: #888; font-style: italic; }
    .lightbox-text-content { flex: 1; overflow: auto; background: #f8f9fc; border-radius: 8px; padding: 12px; font-size: 0.88rem; line-height: 1.6; white-space: pre-wrap; word-break: break-word; margin: 0; max-height: 65vh; border: 1px solid #e0e3ef; }
    .btn-reply-small { font-size: 0.78rem; background: #e8eaf6; border: none; border-radius: 6px; padding: 3px 10px; cursor: pointer; color: #3247ff; }
    .btn-reply-small:hover { background: #c5caff; }
    .replies { padding: 6px 0 0 16px; border-left: 3px solid #e8eaf6; margin-top: 4px; }
    .pagination { display: flex; align-items: center; gap: 12px; margin-top: 14px; }
    .btn-page { background: #e8eaf6; color: #3247ff; border: none; border-radius: 8px; padding: 7px 14px; cursor: pointer; font-size: 0.88rem; }
    .btn-page:hover { background: #c5caff; }
    .btn-page:disabled { opacity: .4; cursor: not-allowed; }
    .page-info { font-size: 0.88rem; color: #555; }
    .reply-banner { background: #e8f5e9; border-radius: 8px; padding: 8px 12px; font-size: 0.88rem; display: flex; align-items: center; gap: 10px; }
    .btn-cancel-reply { margin-left: auto; background: none; border: none; color: #e53935; cursor: pointer; font-size: 0.85rem; }
    .lightbox-overlay { position: fixed; inset: 0; background: rgba(0,0,0,.78); display: flex; align-items: center; justify-content: center; z-index: 9999; animation: fadeIn .2s; }
    .lightbox-box { position: relative; max-width: 90vw; max-height: 90vh; background: white; border-radius: 12px; padding: 16px; box-shadow: 0 8px 40px rgba(0,0,0,.5); animation: zoomIn .22s; }
    .lightbox-close { position: absolute; top: 10px; right: 12px; background: none; border: none; font-size: 1.5rem; cursor: pointer; color: #555; line-height: 1; }
    .lightbox-img { max-width: 80vw; max-height: 75vh; display: block; border-radius: 8px; }
    .lightbox-caption { text-align: center; font-size: 0.85rem; color: #666; margin-top: 8px; }
    /* preview section fade-in */
    .preview-card { animation: slideDown .25s ease; }
    /* new comment highlight */
    .row-highlight td { animation: rowFlash 2s ease; }
    /* submit spinner */
    .spinner { display: inline-block; width: 13px; height: 13px; border: 2px solid rgba(255,255,255,.4); border-top-color: white; border-radius: 50%; animation: spin .6s linear infinite; margin-right: 6px; vertical-align: middle; }
    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
    @keyframes zoomIn { from { transform: scale(.82); opacity: 0; } to { transform: scale(1); opacity: 1; } }
    @keyframes slideDown { from { opacity: 0; transform: translateY(-8px); } to { opacity: 1; transform: translateY(0); } }
    @keyframes rowFlash { 0%,100% { background: transparent; } 20% { background: #e8f5e9; } }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class AppComponent implements OnInit, OnDestroy {
  readonly apiBase = API_BASE;

  form!: FormGroup;

  @ViewChild('messageBox') messageBox!: ElementRef<HTMLTextAreaElement>;

  selectedFile: File | null = null;
  captchaChallengeId = '';
  captchaImageBase64 = '';
  previewHtml = '';
  serverError = '';
  comments: CommentItem[] = [];
  page = 1;
  pageSize = 25;
  total = 0;
  loading = false;
  sortBy = 'createdAt';
  sortDirection = 'desc';
  replyTarget: CommentItem | null = null;

  searchQuery = '';
  searchResults: { userName: string; email: string; text: string }[] | null = null;

  newCommentId = '';

  lightboxUrl = '';
  lightboxAlt = '';

  textLightboxOpen = false;
  textLightboxName = '';
  textLightboxContent = '';
  textLightboxLoading = false;

  wsConnected = false;
  private hubConnection: signalR.HubConnection | null = null;

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  constructor(private readonly fb: FormBuilder) {
    this.form = this.fb.group({
      userName: ['', [Validators.required, Validators.pattern(/^[a-zA-Z0-9]+$/)]],
      email: ['', [Validators.required, Validators.email]],
      homePage: ['', [Validators.pattern(/^(https?:\/\/[^\s]+)?$/)]],
      text: ['', [Validators.required]],
      captchaCode: ['', [Validators.required, Validators.pattern(/^[a-zA-Z0-9]+$/)]]
    });
  }

  ngOnInit(): void {
    void this.loadCaptcha();
    void this.loadComments();
    this.connectSignalR();
  }

  ngOnDestroy(): void {
    void this.hubConnection?.stop();
  }

  private connectSignalR(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.apiBase}/hubs/comments`)
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('CommentCreated', (comment: CommentItem) => {
      if (!comment.parentCommentId) {
        this.total++;
        if (this.page === 1 && this.sortDirection === 'desc') {
          this.comments = [comment, ...this.comments.slice(0, this.pageSize - 1)];
          this.flashHighlight(comment.id);
        }
      } else {
        this.insertReplyInTree(this.comments, comment);
      }
    });

    this.hubConnection.onreconnected(() => { this.wsConnected = true; });
    this.hubConnection.onclose(() => { this.wsConnected = false; });

    this.hubConnection.start()
      .then(() => { this.wsConnected = true; })
      .catch(() => { this.wsConnected = false; });
  }

  private flashHighlight(id: string): void {
    this.newCommentId = id;
    setTimeout(() => { this.newCommentId = ''; }, 2000);
  }

  private insertReplyInTree(items: CommentItem[], reply: CommentItem): boolean {
    for (const item of items) {
      if (item.id === reply.parentCommentId) {
        item.replies = [...item.replies, reply];
        return true;
      }
      if (this.insertReplyInTree(item.replies, reply)) {
        return true;
      }
    }
    return false;
  }

  ctrl(name: string): AbstractControl {
    return this.form.controls[name as keyof typeof this.form.controls];
  }

  touched(name: string): boolean {
    return this.ctrl(name).touched;
  }

  async loadCaptcha(): Promise<void> {
    const res = await fetch(`${this.apiBase}/api/captcha/new`);
    const data = await res.json();
    this.captchaChallengeId = data.challengeId;
    this.captchaImageBase64 = data.imageBase64;
    this.form.controls["captchaCode"].reset('');
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
  }

  clearFile(fileInput: HTMLInputElement): void {
    this.selectedFile = null;
    fileInput.value = '';
  }

  wrapTag(tag: string): void {
    const textarea = this.messageBox?.nativeElement;
    if (!textarea) {
      const current = this.form.controls["text"].value ?? '';
      this.form.controls["text"].setValue(`${current}<${tag}></${tag}>`);
      return;
    }
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const current = this.form.controls["text"].value ?? '';
    const selected = current.slice(start, end);
    const replaced = current.slice(0, start) + `<${tag}>${selected}</${tag}>` + current.slice(end);
    this.form.controls["text"].setValue(replaced);
    setTimeout(() => {
      textarea.selectionStart = start + tag.length + 2;
      textarea.selectionEnd = start + tag.length + 2 + selected.length;
      textarea.focus();
    });
  }

  insertLink(): void {
    const textarea = this.messageBox?.nativeElement;
    const current = this.form.controls["text"].value ?? '';
    const snippet = `<a href="https://example.com" title="link title">link text</a>`;
    if (!textarea) {
      this.form.controls["text"].setValue(`${current}${snippet}`);
      return;
    }
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selected = current.slice(start, end);
    const tag = selected
      ? `<a href="https://example.com" title="link title">${selected}</a>`
      : snippet;
    const replaced = current.slice(0, start) + tag + current.slice(end);
    this.form.controls["text"].setValue(replaced);
    setTimeout(() => textarea.focus());
  }

  async preview(): Promise<void> {
    const text = this.form.controls["text"].value ?? '';
    if (!text.trim()) {
      this.previewHtml = '';
      return;
    }
    const res = await fetch(`${this.apiBase}/api/comments/preview`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text })
    });
    this.previewHtml = res.ok ? (await res.json()).html : '<em style="color:red">Invalid markup.</em>';
  }

  startReply(comment: CommentItem): void {
    this.replyTarget = comment;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelReply(): void {
    this.replyTarget = null;
  }

  async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.captchaChallengeId) {
      return;
    }

    this.loading = true;
    this.serverError = '';
    const payload = new FormData();
    payload.set('userName', this.form.controls["userName"].value ?? '');
    payload.set('email', this.form.controls["email"].value ?? '');
    const hp = this.form.controls["homePage"].value;
    if (hp) payload.set('homePage', hp);
    payload.set('text', this.form.controls["text"].value ?? '');
    payload.set('captchaChallengeId', this.captchaChallengeId);
    payload.set('captchaCode', this.form.controls["captchaCode"].value ?? '');
    if (this.replyTarget) {
      payload.set('parentCommentId', this.replyTarget.id);
    }
    if (this.selectedFile) {
      payload.set('attachment', this.selectedFile);
    }

    const res = await fetch(`${this.apiBase}/api/comments`, { method: 'POST', body: payload });
    this.loading = false;

    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      this.serverError = err.message ?? 'An error occurred. Please try again.';
      await this.loadCaptcha();
      return;
    }

    this.form.reset();
    this.selectedFile = null;
    this.previewHtml = '';
    this.replyTarget = null;
    await this.loadCaptcha();
    await this.loadComments();
  }

  async loadComments(): Promise<void> {
    const params = new URLSearchParams({
      page: this.page.toString(),
      pageSize: this.pageSize.toString(),
      sortBy: this.sortBy,
      sortDirection: this.sortDirection
    });
    const res = await fetch(`${this.apiBase}/api/comments?${params}`);
    const data = await res.json();
    this.comments = data.items ?? [];
    this.total = data.total ?? 0;
  }

  setSort(column: string): void {
    this.sortBy = column;
    this.sortDirection = 'desc';
    this.page = 1;
    void this.loadComments();
  }

  toggleDir(): void {
    this.sortDirection = this.sortDirection === 'desc' ? 'asc' : 'desc';
    this.page = 1;
    void this.loadComments();
  }

  changePage(delta: number): void {
    const next = this.page + delta;
    if (next < 1 || next > this.totalPages) return;
    this.page = next;
    void this.loadComments();
  }

  async runSearch(): Promise<void> {
    if (!this.searchQuery.trim()) return;
    const res = await fetch(`${this.apiBase}/api/comments/search?q=${encodeURIComponent(this.searchQuery)}`);
    if (res.ok) {
      const data = await res.json();
      this.searchResults = data.items ?? [];
    }
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.searchResults = null;
  }

  openLightbox(url: string, alt: string): void {
    this.lightboxUrl = url;
    this.lightboxAlt = alt;
  }

  closeLightbox(): void {
    this.lightboxUrl = '';
    this.lightboxAlt = '';
  }

  async openTextLightbox(att: { storedFileName: string; originalFileName: string }): Promise<void> {
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
