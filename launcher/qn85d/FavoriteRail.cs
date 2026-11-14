public sealed class FavoriteRail {
 public int ViewportWidth {get;} public int LeftInset {get;} public int ItemWidth {get;} public int Step {get;} public int Count {get;} public int Scroll {get;private set;}
 readonly int endInset;
 public int ContentWidth=>Math.Max(ViewportWidth,Count==0?0:LeftInset+(Count-1)*Step+ItemWidth+endInset);
 public FavoriteRail(int viewportWidth,int leftInset,int itemWidth,int step,int count,int scroll=0,int endInset=20){if(viewportWidth<=0||itemWidth<=0||step<itemWidth||count<0)throw new ArgumentOutOfRangeException();ViewportWidth=viewportWidth;LeftInset=leftInset;ItemWidth=itemWidth;Step=step;Count=count;this.endInset=endInset;Scroll=Math.Clamp(scroll,0,ContentWidth-ViewportWidth);}
 public int Left(int index)=>LeftInset+index*Step;
 public bool Reveal(int index){if(index<0||index>=Count)return false;int old=Scroll,left=Left(index),right=left+ItemWidth;int next=Scroll,margin=Math.Min(12,endInset);if(left<Scroll+margin)next=left-margin;else if(right>Scroll+ViewportWidth-margin)next=right-ViewportWidth+margin;if(index==0)next=0;else if(index==Count-1)next=ContentWidth-ViewportWidth;Scroll=Math.Clamp(next,0,ContentWidth-ViewportWidth);return old!=Scroll;}
 public bool IsVisible(int index)=>index>=0&&index<Count&&Left(index)+ItemWidth>Scroll&&Left(index)<Scroll+ViewportWidth;
}
