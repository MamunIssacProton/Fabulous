﻿namespace Elmish.XamarinForms
  
open System
open Xamarin.Forms

type internal Getter<'model> = 
    'model -> obj

type internal Setter<'model, 'msg> = 
    obj -> 'model -> 'msg

type internal Execute<'model, 'msg> = 
    obj -> 'model -> 'msg

type internal CanExecute<'model> = 
    obj -> 'model -> bool

type internal ValidSetter<'model,'msg> = obj -> 'model -> Choice<'msg,string>

type ViewBinding<'model, 'msg> = 
    string * Variable<'model, 'msg>

and ViewBindings<'model, 'msg> = 
    ViewBinding<'model, 'msg> list

and Variable<'model,'msg> =
    internal 
    | Bind of Getter<'model>
    | BindTwoWay of Getter<'model> * Setter<'model,'msg>
    | BindTwoWayValidation of Getter<'model> * ValidSetter<'model,'msg>
    | BindCmd of Execute<'model,'msg> * CanExecute<'model>
    | BindModel of Getter<'model> * ViewBindings<'model,'msg>
    | BindMap of Getter<'model> * (obj -> obj)

[<RequireQualifiedAccess>]
module Binding =
    
    // Maps a set of view bindings to its parent view bindings
    let rec private mapViewBinding<'model,'msg,'_model,'_msg> toModel toMsg (viewBinding: ViewBindings<'_model,'_msg>) : ViewBindings<'model,'msg> =
        let mapVariable =
            function
            | Bind getter ->
                toModel >> getter
                |> Bind
            | BindTwoWay (getter,setter) -> 
                (toModel >> getter, fun v m -> (toModel m) |> setter v |> toMsg)
                |> BindTwoWay
            | BindTwoWayValidation (getter,setter) -> 
                (toModel >> getter, fun v m -> (toModel m) |> setter v |> function Choice1Of2 r -> Choice1Of2 (toMsg r) | Choice2Of2 err -> Choice2Of2 err)
                |> BindTwoWayValidation
            | BindCmd (exec,canExec) ->
                ((fun v m -> (toModel m) |> exec v |> toMsg), (fun v m -> (toModel m) |> canExec v))
                |> BindCmd
            | BindModel (getter,binding) ->
                (toModel >> getter, binding |> mapViewBinding toModel toMsg)
                |> BindModel
            | BindMap (getter,mapper) ->
                ((toModel >> getter), mapper)
                |> BindMap

        viewBinding
        |> List.map (fun (n,v) -> n, mapVariable v)

    // Helper functions that clean up binding creation

    ///<summary>Source to target binding (i.e. BindingMode.OneWay)</summary>
    ///<param name="getter">Gets value from the model</param>
    ///<param name="name">Binding name</param>
    let oneWay (getter: 'model -> 'a) name : ViewBinding<'model,'msg> = 
        name, Bind (getter >> unbox)
    
    ///<summary>Either source to target or target to source (i.e. BindingMode.TwoWay)</summary>
    ///<param name="getter">Gets value from the model</param>
    ///<param name="setter">Setter function, returns a message to dispatch, typically to set the value in the model</param>
    ///<param name="name">Binding name</param>
    let twoWay (getter: 'model -> 'a) (setter: 'a -> 'model -> 'msg) name : ViewBinding<'model,'msg> = 
        name, BindTwoWay (getter >> unbox, fun v m -> setter (v :?> 'a) m)
    
    ///<summary>Either source to target or target to source (i.e. BindingMode.TwoWay) with INotifyDataErrorInfo implementation)</summary>
    ///<param name="getter">Gets value from the model</param>
    ///<param name="setter">Validation function, returns a Result with the message to dispatch or an error string</param>
    ///<param name="name">Binding name</param>
    let twoWayValidation (getter: 'model -> 'a) (setter: 'a -> 'model -> Choice<'msg,string>) name : ViewBinding<'model,'msg> = 
        name, BindTwoWayValidation (getter >> unbox, fun v m -> setter (v :?> 'a) m)
        
    ///<summary>Command binding</summary>
    ///<param name="exec">Execute function, returns a message to dispatch</param>
    ///<param name="name">Binding name</param>
    let cmd exec name : ViewBinding<'model,'msg> = 
        name, BindCmd (exec, fun _ _ -> true)
        
    ///<summary>Conditional command binding</summary>
    ///<param name="exec">Execute function, returns a message to dispatch</param>
    ///<param name="canExec">CanExecute function, returns a bool</param>
    ///<param name="name">Binding name</param>
    let cmdIf exec canExec name : ViewBinding<'model,'msg> = 
        name, BindCmd (exec, canExec)
        
    ///<summary>Sub-view binding</summary>
    ///<param name="getter">Gets the sub-model from the base model</param>
    ///<param name="viewBinding">Set of view bindings for the sub-view</param>
    ///<param name="toMsg">Maps sub-messages to the base message type</param>
    ///<param name="name">Binding name</param>
    let model (getter: 'model -> '_model) (viewBinding: ViewBindings<'_model,'_msg>) (toMsg: '_msg -> 'msg) name : ViewBinding<'model,'msg> = 
        name, BindModel (getter >> unbox, viewBinding |> mapViewBinding getter toMsg)
        
    ///<summary>One-way binding that applies a map when passing data to the view.
    /// Should be used for data that a view needs wrapped in some view-specific type. 
    /// For example when graphing a series, the data can be stored as a plain array in the model, 
    /// and then mapped to a SeriesCollection for the view.</summary>
    ///<param name="getter">Gets the value from the model</param>
    ///<param name="mapper">Maps the value for consumption by the view</param>
    ///<param name="name">Binding name</param>
    let oneWayMap (getter: 'model -> 'a) (mapper: 'a -> 'b) name : ViewBinding<'model,'msg> =
        name, BindMap (getter >> unbox, unbox >> mapper >> unbox)