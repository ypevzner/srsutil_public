using System;

namespace FDA.SRS.ObjectModel
{
        public class Optional<T> {
            private T tt;
            private Boolean has = false;

            private Optional(T t){
                tt = t;
                if (tt != null){
                    has = true;
                }
            }

            private Optional(){}

            public T get(){
                if (tt == null) throw new Exception("No value present");
                return tt;
            }

            public static Optional<T> ofNullable(T t){
                return new Optional<T>(t);
            }
            public static Optional<T> of(T t){
                Optional<T> op = new Optional<T>(t);
                op.get();
                return op;
            }
            public static Optional<T> empty(){
                return new Optional<T>();
            }


            public Boolean isPresent(){
                return tt != null;
            }

            public Optional<U> map<U>(Func<T,U> fun){
                if (this.isPresent()){
                    return Optional<U>.of(fun.Invoke(this.get()));
                }else{
                    return Optional<U>.empty();
                }
            }

            public T orElse(T orelse){
                if (this.isPresent()) return this.get();
                return orelse;
            }



        public void ifPresent(Action<T> act){
                if(isPresent())
                    act.Invoke(tt);
            }

        }    
    


}
